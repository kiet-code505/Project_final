using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TourismMvc.Localization;

public sealed class UserTranslationCatalog
{
    public IReadOnlyDictionary<string, string> English { get; }
    public IReadOnlyList<(Regex Pattern, string Translation)> Patterns { get; }
    public UserTranslationCatalog(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "Localization", "user.en.json");
        English = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        var admin = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(environment.ContentRootPath, "Localization", "admin.en.json")))!;
        English = English.Concat(admin).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.First().Value);
        Patterns = English.Where(x => !string.IsNullOrEmpty(x.Value) && Regex.IsMatch(x.Key, @"\{\d+\}"))
            .Select(x => (BuildPattern(x.Key), x.Value)).ToArray();
    }
    private static Regex BuildPattern(string key)
    {
        var pattern = Regex.Escape(key);
        foreach (Match match in Regex.Matches(key, @"\{(\d+)\}"))
            pattern = pattern.Replace(Regex.Escape(match.Value), "(?<arg" + match.Groups[1].Value + ">.*?)", StringComparison.Ordinal);
        return new Regex("^" + pattern + "$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}

public sealed class UserText(UserTranslationCatalog catalog, IHttpContextAccessor accessor)
{
    public bool IsAdmin => accessor.HttpContext?.Request.Path.StartsWithSegments("/admin") == true;
    public bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "vi";
    public string Language => IsEnglish ? "en" : "vi";
    public CultureInfo Culture => CultureInfo.GetCultureInfo(IsEnglish ? "en-GB" : "vi-VN");
    public string DateFormat => IsEnglish ? "dd MMM yyyy" : "dd/MM/yyyy";
    public string DateTimeFormat => IsEnglish ? "dd MMM yyyy HH:mm" : "dd/MM/yyyy HH:mm";
    public string TimeDateFormat => IsEnglish ? "HH:mm dd MMM yyyy" : "HH:mm dd/MM/yyyy";
    public string this[string source] => Translate(source);
    public string this[string source, params object[] arguments] => string.Format(Culture, Translate(source), arguments);
    public string Translate(string? source)
    {
        if (string.IsNullOrEmpty(source) || !IsEnglish) return source ?? "";
        var key = Regex.Replace(source.Trim(), @"\s+", " ");
        if (catalog.English.TryGetValue(key, out var translated) && !string.IsNullOrEmpty(translated)) return translated;
        foreach (var (pattern, template) in catalog.Patterns)
        {
            var match = pattern.Match(key);
            if (match.Success) return Regex.Replace(template, @"\{(\d+)\}", m => match.Groups["arg" + m.Groups[1].Value].Value);
        }
        return source;
    }
    public string ClientJson => JsonSerializer.Serialize(catalog.English);
    public static bool IsUserRequest(HttpContext context) => !context.Request.Path.StartsWithSegments("/payment/vnpay/ipn");
}
