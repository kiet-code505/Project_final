using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Helpers;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public class LocationService(
    AppDbContext db,
    IWebHostEnvironment env,
    ILogger<LocationService> logger) : ILocationService
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly int[] AllowedPageSizes = [10, 20, 50];

    public async Task<LocationIndexViewModel> GetPagedLocationsAsync(
        string? search,
        Region? region,
        string sortBy,
        string sortDir,
        int page,
        int pageSize)
    {
        if (!AllowedPageSizes.Contains(pageSize)) pageSize = 10;
        bool asc = sortDir.Equals("ASC", StringComparison.OrdinalIgnoreCase);

        var q = db.Locations.AsNoTracking().AsQueryable();

        // 1. Tìm kiếm (tên, slug, mô tả) không phân biệt hoa thường

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(l => EF.Functions.ILike(l.LocationName, pattern)
                          || EF.Functions.ILike(l.Slug, pattern)
                          || (l.Description != null && EF.Functions.ILike(l.Description, pattern)));
        }

        // 2. Lọc theo vùng miền
        if (region.HasValue)
        {
            q = q.Where(l => l.Region == region);
        }

        // 3. Sắp xếp
        q = sortBy switch
        {
            "name" => asc ? q.OrderBy(l => l.LocationName) : q.OrderByDescending(l => l.LocationName),
            "id" => asc ? q.OrderBy(l => l.LocationID) : q.OrderByDescending(l => l.LocationID),
            _ => asc
                ? q.OrderBy(l => l.UpdatedAt == null).ThenBy(l => l.UpdatedAt).ThenBy(l => l.LocationID)
                : q.OrderBy(l => l.UpdatedAt == null).ThenByDescending(l => l.UpdatedAt).ThenByDescending(l => l.LocationID),
        };

        // 4. Phân trang
        var total = await q.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var locations = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // 5. Đếm số tour đang dùng làm điểm đi / điểm đến
        var ids = locations.Select(l => l.LocationID).ToList();
        var startCounts = await db.Tours
            .Where(t => ids.Contains(t.StartLocationId))
            .GroupBy(t => t.StartLocationId)
            .Select(g => new { g.Key, C = g.LongCount() })
            .ToDictionaryAsync(x => x.Key, x => x.C);

        var endCounts = await db.Tours
            .Where(t => ids.Contains(t.EndLocationId))
            .GroupBy(t => t.EndLocationId)
            .Select(g => new { g.Key, C = g.LongCount() })
            .ToDictionaryAsync(x => x.Key, x => x.C);

        // 6. Thống kê tổng quan
        var stats = await db.Locations
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                North = g.Count(l => l.Region == Region.NORTH),
                Central = g.Count(l => l.Region == Region.CENTRAL),
                South = g.Count(l => l.Region == Region.SOUTH),
                Airport = g.Count(l => l.AirportCode != null && l.AirportCode != ""),
            }).FirstOrDefaultAsync();

        return new LocationIndexViewModel
        {
            Items = locations.Select(l => new LocationListItem
            {
                Location = l,
                ToursAsStartPoint = startCounts.GetValueOrDefault(l.LocationID),
                ToursAsEndPoint = endCounts.GetValueOrDefault(l.LocationID),
            }).ToList(),
            Search = search,
            Region = region,
            SortBy = sortBy,
            SortDir = asc ? "ASC" : "DESC",
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            StatTotal = stats?.Total ?? 0,
            StatNorth = stats?.North ?? 0,
            StatCentral = stats?.Central ?? 0,
            StatSouth = stats?.South ?? 0,
            StatWithAirport = stats?.Airport ?? 0,
        };
    }

    public async Task<LocationFormViewModel?> GetForEditAsync(int id)
    {
        var l = await db.Locations.AsNoTracking().FirstOrDefaultAsync(x => x.LocationID == id);
        if (l == null) return null;

        return new LocationFormViewModel
        {
            LocationID = l.LocationID,
            Name = l.LocationName,
            Slug = l.Slug,
            Region = l.Region,
            Description = l.Description,
            AirportCode = l.AirportCode,
            AirportName = l.AirportName,
            ImageURL = l.ImageURL,
        };
    }

    public async Task<bool> CreateAsync(LocationFormViewModel vm, ModelStateDictionary modelState)
    {
        Normalize(vm);
        await ValidateUniqueAsync(vm, modelState);
        ValidateImage(vm.ImageFile, modelState);

        if (!modelState.IsValid) return false;

        var location = new Location();
        Apply(vm, location);

        if (vm.ImageFile != null)
        {
            location.ImageURL = await SaveImageAsync(vm.ImageFile);
        }

        db.Locations.Add(location);
        await db.SaveChangesAsync();
        logger.LogInformation("LocationService: Created location {Id} - {Name}", location.LocationID, location.LocationName);

        vm.LocationID = location.LocationID;
        return true;
    }

    public async Task<bool> UpdateAsync(int id, LocationFormViewModel vm, ModelStateDictionary modelState)
    {
        var location = await db.Locations.FirstOrDefaultAsync(x => x.LocationID == id);
        if (location == null) return false;

        vm.LocationID = id;
        vm.ImageURL = location.ImageURL;

        Normalize(vm);
        await ValidateUniqueAsync(vm, modelState);
        ValidateImage(vm.ImageFile, modelState);

        if (!modelState.IsValid) return false;

        Apply(vm, location);

        if (vm.ImageFile != null)
        {
            var oldImage = location.ImageURL;
            location.ImageURL = await SaveImageAsync(vm.ImageFile);
            DeleteLocalImage(oldImage);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("LocationService: Updated location {Id} - {Name}", location.LocationID, location.LocationName);
        return true;
    }

    public async Task<(int DeletedCount, List<string> BlockedReasons)> DeleteLocationsAsync(int[] ids)
    {
        if (ids.Length == 0) return (0, []);

        var locations = await db.Locations.Where(l => ids.Contains(l.LocationID)).ToListAsync();
        var inUse = await db.Tours
            .Where(t => ids.Contains(t.StartLocationId) || ids.Contains(t.EndLocationId))
            .Select(t => new { t.StartLocationId, t.EndLocationId })
            .ToListAsync();

        var blocked = new List<string>();
        var deletable = new List<Location>();

        foreach (var l in locations)
        {
            var startCount = inUse.Count(t => t.StartLocationId == l.LocationID);
            var endCount = inUse.Count(t => t.EndLocationId == l.LocationID);

            if (startCount > 0 || endCount > 0)
            {
                blocked.Add($"\"{l.LocationName}\" ({startCount} tour xuất phát, {endCount} tour điểm đến)");
            }
            else
            {
                deletable.Add(l);
            }
        }

        if (deletable.Count > 0)
        {
            db.Locations.RemoveRange(deletable);  
            await db.SaveChangesAsync();
            deletable.ForEach(l => DeleteLocalImage(l.ImageURL));
            logger.LogInformation("LocationService: Deleted {Count} locations", deletable.Count);
        }

        return (deletable.Count, blocked);
    }

    public async Task<List<Location>> GetLocationsByRegionAsync(Region region)
    {
        return await db.Locations
            .AsNoTracking()
            .Where(l => l.Region == region && l.Status)
            .OrderBy(l => l.LocationName)
            .ToListAsync();
    }

    public async Task<List<Location>> GetUniqueStartLocationsAsync()
    {
        return await db.Tours
            .AsNoTracking()
            .Where(t => t.Status)
            .Select(t => t.StartLocation!)
            .Where(l => l != null && l.Status)
            .Distinct()
            .OrderBy(l => l.LocationName)
            .ToListAsync();
    }

    public async Task<List<Location>> GetUniqueEndLocationsAsync()
    {
        return await db.Tours
            .AsNoTracking()
            .Where(t => t.Status)
            .Select(t => t.EndLocation!)
            .Where(l => l != null && l.Status)
            .Distinct()
            .OrderBy(l => l.LocationName)
            .ToListAsync();
    }

    // ───────────────────────── Helper Methods ─────────────────────────

    private static void Normalize(LocationFormViewModel vm)
    {
        vm.Name = vm.Name?.Trim() ?? "";
        vm.Slug = string.IsNullOrWhiteSpace(vm.Slug)
            ? SlugHelper.Generate(vm.Name)
            : vm.Slug.Trim().ToLowerInvariant();

        vm.AirportCode = string.IsNullOrWhiteSpace(vm.AirportCode) ? null : vm.AirportCode.Trim().ToUpperInvariant();
        vm.AirportName = string.IsNullOrWhiteSpace(vm.AirportName) ? null : vm.AirportName.Trim();
        vm.Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();

        // Tự điền thông tin sân bay nếu còn trống
        if (vm.AirportCode == null || vm.AirportName == null)
        {
            var airport = VietnamAirports.Find(vm.Name);
            if (airport != null)
            {
                vm.AirportCode ??= airport.Code;
                vm.AirportName ??= airport.Name;
            }
        }
    }

    private async Task ValidateUniqueAsync(LocationFormViewModel vm, ModelStateDictionary modelState)
    {
        var id = vm.LocationID ?? 0;

        if (await db.Locations.AnyAsync(l => l.LocationID != id && l.LocationName == vm.Name))
        {
            modelState.AddModelError(nameof(vm.Name), $"Địa điểm \"{vm.Name}\" đã tồn tại");
        }

        if (string.IsNullOrEmpty(vm.Slug))
        {
            modelState.AddModelError(nameof(vm.Slug), "Slug không được để trống");
        }
        else if (await db.Locations.AnyAsync(l => l.LocationID != id && l.Slug == vm.Slug))
        {
            modelState.AddModelError(nameof(vm.Slug), $"Slug \"{vm.Slug}\" đã được sử dụng");
        }
    }

    private static void ValidateImage(IFormFile? file, ModelStateDictionary modelState)
    {
        if (file == null) return;

        if (!file.ContentType.StartsWith("image/"))
        {
            modelState.AddModelError(nameof(LocationFormViewModel.ImageFile), "Vui lòng chọn đúng định dạng file ảnh");
        }
        else if (file.Length > MaxImageBytes)
        {
            modelState.AddModelError(nameof(LocationFormViewModel.ImageFile), "Kích thước ảnh tối đa là 5MB");
        }
    }

    private static void Apply(LocationFormViewModel vm, Location l)
    {
        l.LocationName = vm.Name;
        l.Slug = vm.Slug!;
        l.Region = vm.Region!.Value;
        l.Description = vm.Description;
        l.AirportCode = vm.AirportCode;
        l.AirportName = vm.AirportName;
    }

    private async Task<string> SaveImageAsync(IFormFile file)
    {
        var dir = Path.Combine(env.WebRootPath, "uploads", "locations");
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || ext.Length > 6) ext = ".jpg";

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(dir, fileName);

        await using var fs = System.IO.File.Create(filePath);
        await file.CopyToAsync(fs);

        return $"/uploads/locations/{fileName}";
    }

    private void DeleteLocalImage(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/locations/")) return;

        try
        {
            var relativePath = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(env.WebRootPath, relativePath);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể xóa ảnh cũ: {Url}", url);
        }
    }
}
