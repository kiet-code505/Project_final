using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public class BranchContactService(
    AppDbContext db,
    ILogger<BranchContactService> logger) : IBranchContactService
{
    public async Task<BranchIndexViewModel> GetPagedBranchesAsync(string? search, int page, int pageSize)
    {
        if (pageSize <= 0) pageSize = 12;
        var q = db.Branches.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(b => EF.Functions.ILike(b.BranchName, pattern)
                          || EF.Functions.ILike(b.Phone, pattern)
                          || EF.Functions.ILike(b.Email, pattern)
                          || EF.Functions.ILike(b.Address, pattern));
        }

        q = q.OrderByDescending(b => b.UpdatedAt == null)
             .ThenByDescending(b => b.UpdatedAt)
             .ThenByDescending(b => b.ContactID);

        var total = await q.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var branches = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var branchIds = branches.Select(b => b.ContactID).ToList();
        var policyCounts = await db.Policies
            .Where(p => p.ContactId.HasValue && branchIds.Contains(p.ContactId.Value))
            .GroupBy(p => p.ContactId!.Value)
            .Select(g => new { ContactId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ContactId, x => x.Count);

        var totalPolicies = await db.Policies.CountAsync();

        return new BranchIndexViewModel
        {
            Items = branches.Select(b => new BranchListItem
            {
                Branch = b,
                PolicyCount = policyCounts.GetValueOrDefault(b.ContactID)
            }).ToList(),
            Search = search,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            StatTotal = await db.Branches.CountAsync(),
            StatTotalPolicies = totalPolicies
        };
    }

    public async Task<BranchFormViewModel?> GetForEditAsync(int id)
    {
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.ContactID == id);
        if (branch == null) return null;

        return new BranchFormViewModel
        {
            ContactID = branch.ContactID,
            BranchName = branch.BranchName,
            Phone = branch.Phone,
            Email = branch.Email,
            Address = branch.Address
        };
    }

    public async Task<List<BranchContact>> GetAllActiveBranchesAsync()
    {
        return await db.Branches.AsNoTracking()
            .OrderBy(b => b.BranchName)
            .ToListAsync();
    }

    public async Task<bool> CreateAsync(BranchFormViewModel vm, ModelStateDictionary modelState)
    {
        Normalize(vm);
        await ValidateUniqueAsync(vm, modelState);
        if (!modelState.IsValid) return false;

        var branch = new BranchContact
        {
            BranchName = vm.BranchName,
            Phone = vm.Phone,
            Email = vm.Email,
            Address = vm.Address
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        logger.LogInformation("BranchContactService: Created branch {Id} - {Name}", branch.ContactID, branch.BranchName);

        vm.ContactID = branch.ContactID;
        return true;
    }

    public async Task<bool> UpdateAsync(int id, BranchFormViewModel vm, ModelStateDictionary modelState)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.ContactID == id);
        if (branch == null) return false;

        vm.ContactID = id;
        Normalize(vm);
        await ValidateUniqueAsync(vm, modelState);
        if (!modelState.IsValid) return false;

        branch.BranchName = vm.BranchName;
        branch.Phone = vm.Phone;
        branch.Email = vm.Email;
        branch.Address = vm.Address;

        await db.SaveChangesAsync();
        logger.LogInformation("BranchContactService: Updated branch {Id} - {Name}", branch.ContactID, branch.BranchName);
        return true;
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(int id)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.ContactID == id);
        if (branch == null) return (false, "Không tìm thấy chi nhánh cần xóa.");

        var policyCount = await db.Policies.CountAsync(p => p.ContactId == id);
        if (policyCount > 0)
        {
            return (false, $"Không thể xóa chi nhánh \"{branch.BranchName}\" vì đang được liên kết bởi {policyCount} mẫu chính sách.");
        }

        db.Branches.Remove(branch);
        await db.SaveChangesAsync();
        logger.LogInformation("BranchContactService: Deleted branch {Id} - {Name}", branch.ContactID, branch.BranchName);
        return (true, null);
    }

    private static void Normalize(BranchFormViewModel vm)
    {
        vm.BranchName = vm.BranchName?.Trim() ?? "";
        vm.Phone = vm.Phone?.Trim() ?? "";
        vm.Email = vm.Email?.Trim().ToLowerInvariant() ?? "";
        vm.Address = vm.Address?.Trim() ?? "";
    }

    private async Task ValidateUniqueAsync(BranchFormViewModel vm, ModelStateDictionary modelState)
    {
        var id = vm.ContactID ?? 0;
        if (await db.Branches.AnyAsync(b => b.ContactID != id && b.BranchName == vm.BranchName))
        {
            modelState.AddModelError(nameof(vm.BranchName), $"Chi nhánh \"{vm.BranchName}\" đã tồn tại");
        }
    }
}
