using Microsoft.AspNetCore.Mvc.ModelBinding;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public interface ILocationService
{
    /// <summary>
    /// Lấy danh sách địa điểm phân trang, lọc theo vùng miền, tìm kiếm và thống kê.
    /// </summary>
    Task<LocationIndexViewModel> GetPagedLocationsAsync(
        string? search,
        Region? region,
        string sortBy,
        string sortDir,
        int page,
        int pageSize);

    /// <summary>
    /// Lấy dữ liệu địa điểm để đưa vào form chỉnh sửa.
    /// </summary>
    Task<LocationFormViewModel?> GetForEditAsync(int id);

    /// <summary>
    /// Tạo mới địa điểm (chuẩn hóa dữ liệu, validate trùng tên/slug, upload ảnh).
    /// </summary>
    Task<bool> CreateAsync(LocationFormViewModel vm, ModelStateDictionary modelState);

    /// <summary>
    /// Cập nhật địa điểm theo ID.
    /// </summary>
    Task<bool> UpdateAsync(int id, LocationFormViewModel vm, ModelStateDictionary modelState);

    /// <summary>
    /// Xóa 1 hoặc nhiều địa điểm (kiểm tra ràng buộc các Tour đang dùng làm điểm đi/đến).
    /// </summary>
    Task<(int DeletedCount, List<string> BlockedReasons)> DeleteLocationsAsync(int[] ids);

    /// <summary>
    /// Lấy danh sách địa điểm theo vùng miền (dùng cho trang chủ/dropdown).
    /// </summary>
    Task<List<Location>> GetLocationsByRegionAsync(Region region);

    /// <summary>
    /// Lấy tất cả các điểm khởi hành duy nhất có tour.
    /// </summary>
    Task<List<Location>> GetUniqueStartLocationsAsync();

    /// <summary>
    /// Lấy tất cả các điểm đến duy nhất có tour.
    /// </summary>
    Task<List<Location>> GetUniqueEndLocationsAsync();
}
