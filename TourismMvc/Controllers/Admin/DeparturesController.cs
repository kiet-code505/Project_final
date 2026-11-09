using Microsoft.AspNetCore.Mvc;
using TourismMvc.Models.ViewModels;
using TourismMvc.Services;
using System.Text;
using TourismMvc.Localization;

namespace TourismMvc.Controllers.Admin;

[Route("admin/departures")]
public class DeparturesController(IDepartureService departureService, UserText text) : AdminControllerBase
{
    private const string FormView = "~/Views/Admin/Departures/Form.cshtml";
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery, Bind("Search,TourId,StartDate,EndDate,Status,NewestFirst,Page,PageSize")] DepartureIndexViewModel filter)
    {
        filter.PageSize = filter.PageSize is 10 or 20 or 50 ? filter.PageSize : 10;
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError("", "Bộ lọc không hợp lệ. Vui lòng kiểm tra ngày, trạng thái và phân trang.");
            return View("~/Views/Admin/Departures/Index.cshtml", filter);
        }
        if (filter.StartDate > filter.EndDate)
        {
            ModelState.AddModelError("", "Ngày kết thúc phải từ ngày bắt đầu trở đi.");
            return View("~/Views/Admin/Departures/Index.cshtml", filter);
        }
        return View("~/Views/Admin/Departures/Index.cshtml", await departureService.GetPagedAsync(filter));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, string? returnUrl)
    {
        var vm = await departureService.GetDetailAsync(id);
        if (vm == null) return NotFound(text["Không tìm thấy lịch khởi hành."]);
        vm.ReturnUrl = returnUrl != null && Url.IsLocalUrl(returnUrl) ? returnUrl : "/admin/departures";
        return View("~/Views/Admin/Departures/Details.cshtml", vm);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(int? tourId, string? returnUrl)
    {
        var model = new DepartureFormViewModel { TourId = tourId ?? 0, ReturnUrl = BackUrl(returnUrl) };
        await departureService.PrepareFormAsync(model);
        return View(FormView, model);
    }

    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DepartureFormViewModel model)
    {
        var id = await departureService.SaveAsync(null, model, ModelState);
        if (id.HasValue) { TempData["Success"] = "Đã thêm lịch khởi hành."; return RedirectToAction(nameof(Details), new { id, returnUrl = BackUrl(model.ReturnUrl) }); }
        await departureService.PrepareFormAsync(model);
        return View(FormView, model);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, string? returnUrl)
    {
        var model = await departureService.GetForEditAsync(id);
        if (model == null) { TempData["Error"] = "Không tìm thấy lịch khởi hành."; return RedirectToAction(nameof(Index)); }
        model.ReturnUrl = BackUrl(returnUrl);
        return View(FormView, model);
    }

    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DepartureFormViewModel model)
    {
        if (await departureService.SaveAsync(id, model, ModelState) != null)
        { TempData["Success"] = "Đã cập nhật lịch khởi hành."; return RedirectToAction(nameof(Details), new { id, returnUrl = BackUrl(model.ReturnUrl) }); }
        await departureService.PrepareFormAsync(model);
        return View(FormView, model);
    }

    [HttpGet("{id:int}/clone")]
    public async Task<IActionResult> Clone(int id, string? returnUrl)
    {
        var model = await departureService.GetForCloneAsync(id);
        if (model == null) { TempData["Error"] = "Không tìm thấy lịch khởi hành."; return RedirectToAction(nameof(Index)); }
        model.ReturnUrl = BackUrl(returnUrl);
        return View("~/Views/Admin/Departures/Clone.cshtml", model);
    }

    [HttpPost("{id:int}/clone"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Clone(int id, DepartureCloneViewModel model)
    {
        var newId = await departureService.CloneAsync(id, model, ModelState);
        if (newId.HasValue)
        { TempData["Success"] = "Đã sao chép lịch, bảng giá và vận chuyển sang ngày mới."; return RedirectToAction(nameof(Details), new { id = newId, returnUrl = BackUrl(model.ReturnUrl) }); }
        var original = await departureService.GetForCloneAsync(id);
        if (original == null) { TempData["Error"] = "Lịch gốc không còn tồn tại."; return RedirectToAction(nameof(Index)); }
        model.Id = id; model.TourName = original.TourName; model.OriginalDate = original.OriginalDate;
        return View("~/Views/Admin/Departures/Clone.cshtml", model);
    }

    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var result = await departureService.DeleteAsync([id]);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return LocalRedirect(BackUrl(returnUrl));
    }

    [HttpPost("delete-selected"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSelected(int[] ids, string? returnUrl)
    {
        var result = await departureService.DeleteAsync(ids);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return LocalRedirect(BackUrl(returnUrl));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery, Bind("Search,TourId,StartDate,EndDate,Status,NewestFirst,Page,PageSize")] DepartureIndexViewModel filter)
    {
        if (!ModelState.IsValid || filter.StartDate > filter.EndDate) { TempData["Error"] = "Bộ lọc xuất CSV không hợp lệ."; return RedirectToAction(nameof(Index)); }
        var model = await departureService.GetPagedAsync(filter);
        // Protect spreadsheet cells containing user-authored tour names from formula execution.
        static string Cell(string? value)
        {
            value ??= "";
            if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var rows = new List<string> { text["ID,Mã tour,Tên tour,Ngày khởi hành,Thời lượng,Chỗ trống,Đã đặt,Giá người lớn,Trạng thái"] };
        rows.AddRange(model.Items.Select(d => string.Join(",", new[] { d.DepartureId.ToString(), d.TourCode, d.TourName, d.DepartureDate.ToString(text.DateTimeFormat, text.Culture), d.Duration,
            d.AvailableSlots.ToString(), d.BookedSlots.ToString(), d.LowestPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture), text[d.Active ? "Hoạt động" : "Tạm dừng"] }.Select(Cell))));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join("\r\n", rows))).ToArray(), "text/csv; charset=utf-8", $"lich-khoi-hanh-trang-{model.Page}.csv");
    }

    private string BackUrl(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : "/admin/departures";
}
