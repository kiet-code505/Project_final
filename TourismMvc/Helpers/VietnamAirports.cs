namespace TourismMvc.Helpers;

/// <summary>Port của VietnamAirportUtils.java – tự điền mã/tên sân bay theo tên địa điểm.</summary>
public static class VietnamAirports
{
    public record AirportInfo(string Code, string Name, string CityName);

    private static readonly Dictionary<string, AirportInfo> Map = Build();

    private static Dictionary<string, AirportInfo> Build()
    {
        var m = new Dictionary<string, AirportInfo>();
        void Add(AirportInfo a, params string[] keys) { foreach (var k in keys) m[k] = a; }

        // Miền Bắc
        Add(new("HAN", "Sân bay Quốc tế Nội Bài", "Hà Nội"), "hà nội", "hanoi", "ha noi", "noi bai");
        Add(new("HPH", "Sân bay Quốc tế Cát Bi", "Hải Phòng"), "hải phòng", "hai phong", "cat bi");
        Add(new("DIN", "Sân bay Điện Biên Phủ", "Điện Biên"), "điện biên", "dien bien");
        Add(new("VDO", "Sân bay Quốc tế Vân Đồn", "Quảng Ninh"), "vân đồn", "van don", "quảng ninh", "quang ninh");
        // Miền Trung
        Add(new("THD", "Sân bay Thọ Xuân", "Thanh Hóa"), "thanh hóa", "thanh hoa", "tho xuan");
        Add(new("VII", "Sân bay Quốc tế Vinh", "Nghệ An"), "vinh", "nghệ an", "nghe an");
        Add(new("VDH", "Sân bay Đồng Hới", "Quảng Bình"), "đồng hới", "dong hoi", "quảng bình", "quang binh");
        Add(new("HUI", "Sân bay Quốc tế Phú Bài", "Thừa Thiên Huế"), "huế", "hue", "phu bai", "thừa thiên huế");
        Add(new("DAD", "Sân bay Quốc tế Đà Nẵng", "Đà Nẵng"), "đà nẵng", "da nang", "danang");
        Add(new("VCL", "Sân bay Chu Lai", "Quảng Nam"), "chu lai", "quảng nam", "quang nam");
        Add(new("PXU", "Sân bay Pleiku", "Gia Lai"), "pleiku", "gia lai");
        Add(new("BMV", "Sân bay Buôn Ma Thuột", "Đắk Lắk"), "buôn ma thuột", "buon ma thuot", "dak lak", "đắk lắk");
        Add(new("TBB", "Sân bay Tuy Hòa", "Phú Yên"), "tuy hòa", "tuy hoa", "phú yên", "phu yen");
        // Miền Nam
        Add(new("CXR", "Sân bay Quốc tế Cam Ranh", "Khánh Hòa"), "nha trang", "cam ranh", "khánh hòa", "khanh hoa");
        Add(new("DLI", "Sân bay Liên Khương", "Lâm Đồng"), "đà lạt", "da lat", "dalat", "lâm đồng", "lam dong");
        Add(new("SGN", "Sân bay Quốc tế Tân Sơn Nhất", "TP. Hồ Chí Minh"), "hồ chí minh", "ho chi minh", "sài gòn", "saigon", "tan son nhat");
        Add(new("VTG", "Sân bay Vũng Tàu", "Bà Rịa - Vũng Tàu"), "vũng tàu", "vung tau");
        Add(new("VCS", "Sân bay Côn Đảo", "Bà Rịa - Vũng Tàu"), "côn đảo", "con dao");
        Add(new("VCA", "Sân bay Quốc tế Cần Thơ", "Cần Thơ"), "cần thơ", "can tho");
        Add(new("PQC", "Sân bay Quốc tế Phú Quốc", "Kiên Giang"), "phú quốc", "phu quoc");
        Add(new("VKG", "Sân bay Rạch Giá", "Kiên Giang"), "rạch giá", "rach gia", "kiên giang", "kien giang");
        Add(new("CAH", "Sân bay Cà Mau", "Cà Mau"), "cà mau", "ca mau");
        return m;
    }

    public static AirportInfo? Find(string? locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName)) return null;
        var key = System.Text.RegularExpressions.Regex.Replace(locationName.ToLowerInvariant().Trim(), "\\s+", " ")
            .Replace("tp.", "").Replace("thành phố", "").Replace("tỉnh", "").Trim();
        return Map.GetValueOrDefault(key);
    }

    /// <summary>Dùng cho JS phía client tự điền khi gõ tên.</summary>
    public static IReadOnlyDictionary<string, AirportInfo> All => Map;
}
