namespace TourismMvc.Models.ViewModels;

public class PublicTourSearchFilter
{
    public string? SearchNameTour { get; set; }
    public int? StartLocationID { get; set; }
    public int? EndLocationID { get; set; }
    public string? Budget { get; set; }
    public string? Transportation { get; set; }
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
}

public class PublicTourSearchItem : HomeTourCard
{
    public string Transportation { get; set; } = "";
}

public class PublicTourSearchViewModel
{
    public PublicTourSearchFilter Filter { get; set; } = new();
    public List<PublicTourSearchItem> Items { get; set; } = [];
    public List<HomeDestination> Destinations { get; set; } = [];
    public List<HomeDestination> StartLocations { get; set; } = [];
    public int TotalItems { get; set; }
    public int PageSize => 12;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    public int ActiveFilterCount => (Filter.EndLocationID.HasValue ? 1 : 0)
        + (Filter.StartLocationID.HasValue ? 1 : 0) + (Filter.Budget != null ? 1 : 0)
        + (!string.IsNullOrEmpty(Filter.Transportation) ? 1 : 0);

    public static readonly (string Value, string Label)[] Budgets =
    [ ("under5", "Dưới 5 triệu"), ("5to10", "Từ 5 - 10 triệu"),
      ("10to20", "Từ 10 - 20 triệu"), ("over20", "Trên 20 triệu") ];
    public static readonly string[] Transports = ["Máy bay", "Xe khách", "Tàu hỏa", "Du thuyền"];
}
