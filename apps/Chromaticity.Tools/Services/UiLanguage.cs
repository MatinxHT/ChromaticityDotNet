using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chromaticity.Tools.Services;

/// <summary>Presentation translations; calculation schemas and numeric culture stay unchanged.</summary>
public static class UiLanguage
{
    public const string Chinese = "zh-CN";
    public const string English = "en";
    public static string Current { get; private set; } = Chinese;
    public static event Action? Changed;

    private static readonly Dictionary<string, string> Catalog = LoadCatalog();
    private static readonly (Regex Pattern, string Translation)[] Templates = Catalog
        .Where(pair => Regex.IsMatch(pair.Key, @"\{\d+\}"))
        .OrderByDescending(pair => Regex.Replace(pair.Key, @"\{\d+\}", "").Length)
        .Select(pair => (new Regex("\\A" + Regex.Replace(Regex.Escape(pair.Key), @"\\\{(\d+)\}",
            match => $"(?<p{match.Groups[1].Value}>.*?)") + "\\z", RegexOptions.Singleline), pair.Value)).ToArray();
    private static readonly Regex Fragments = new(string.Join("|", Catalog.Keys
        .Where(key => !Regex.IsMatch(key, @"\{\d+\}"))
        .OrderByDescending(key => key.Length).Select(Regex.Escape)));

    public static void Set(string? language)
    {
        var next = language == English ? English : Chinese;
        if (Current == next) return;
        Current = next;
        Changed?.Invoke();
    }

    public static string Translate(string source) => Translate(source, Current);

    public static string Translate(string source, string language) => language == English ? TranslateEnglish(source, 0) : source;

    private static string TranslateEnglish(string source, int depth)
    {
        if (depth > 8 || !Regex.IsMatch(source, @"\p{IsCJKUnifiedIdeographs}")) return source;
        if (Catalog.TryGetValue(source, out var exact)) return exact;
        foreach (var (pattern, translation) in Templates)
        {
            var match = pattern.Match(source);
            if (!match.Success) continue;
            var count = pattern.GetGroupNames().Count(name => name.StartsWith('p'));
            var arguments = Enumerable.Range(0, count)
                .Select(i => (object)TranslateEnglish(match.Groups[$"p{i}"].Value, depth + 1)).ToArray();
            return string.Format(CultureInfo.InvariantCulture, translation, arguments);
        }
        // Queries and errors can concatenate complete sentences. Translate each sentence
        // separately, then translate static fragments such as axis labels and grade units.
        var sentences = Regex.Matches(source, @"[^。\n]+。?|\n|。");
        if (sentences.Count > 1)
            return string.Concat(sentences.Select(match => TranslateEnglish(match.Value, depth + 1)));
        return Fragments.Replace(source, match => Catalog[match.Value]);
    }

    public static CalculationTable TranslateTable(CalculationTable table) => TranslateTable(table, Current);

    public static CalculationTable TranslateTable(CalculationTable table, string language) => new(
        table.Headers.Select(value => Translate(value, language)).ToArray(),
        table.Rows.Select(row => row.Select(value => Translate(value, language)).ToArray()).ToArray(),
        Translate(table.Conditions, language));

    private static Dictionary<string, string> LoadCatalog()
    {
        using var stream = typeof(UiLanguage).Assembly.GetManifestResourceStream("Chromaticity.Tools.TranslationCatalog.json")!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetString()!);
    }
}
