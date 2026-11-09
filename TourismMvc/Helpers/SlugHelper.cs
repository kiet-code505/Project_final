using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TourismMvc.Helpers;

public static class SlugHelper
{
    /// <summary>"Đà Nẵng" -> "da-nang" (giống generateSlug ở frontend React).</summary>
    public static string Generate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var s = input.Trim().ToLowerInvariant().Replace('đ', 'd');
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        s = Regex.Replace(sb.ToString(), "[^a-z0-9\\s-]", "");
        s = Regex.Replace(s, "\\s+", "-");
        return Regex.Replace(s, "-+", "-").Trim('-');
    }
}
