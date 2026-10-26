using System.Text.Json;
using System.Text.RegularExpressions;
using bitirme.Services.Localization;

namespace bitirme.Tests;

/// <summary>
/// İngilizce desteği (DECISION-49): görünümlerde ve kodda çeviriye verilen her Türkçe metnin Resources/en.json'da karşılığı
/// olmalı; yoksa İngilizce sayfada Türkçe görünür. Ayrıca karşılıklardaki {0} yer tutucuları Türkçesiyle aynı olmalı.
/// </summary>
public partial class LocalizationCoverageTests
{
    private static readonly string AppDirectory = FindAppDirectory();

    private static readonly Dictionary<string, string> English =
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppDirectory, "Resources", "en.json")))!;

    [GeneratedRegex("""(?:(?:\bL)\[|\bL\.GetString\(|SiteText\.T\()\s*"((?:[^"\\]|\\.)*)"(?=\s*[\],)])""")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("""ViewData\["Title"\]\s*=\s*"((?:[^"\\]|\\.)*)"\s*;""")]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    public static TheoryData<string> SourceFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(AppDirectory, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cshtml") || f.EndsWith(".cs"))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                     .Order())
        {
            data.Add(Path.GetRelativePath(AppDirectory, file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void EveryTranslatedText_HasAnEnglishVersion(string file)
    {
        // Yorum satırlarındaki örnekler sayılmaz.
        var source = string.Join(Environment.NewLine, File.ReadAllLines(Path.Combine(AppDirectory, file))
            .Where(line => !line.TrimStart().StartsWith("//") && !line.TrimStart().StartsWith("///")));
        var keys = KeyPattern().Matches(source).Select(m => Unescape(m.Groups[1].Value)).ToList();
        // Çeviriye geçmiş görünümlerde sayfa başlığı da çevrilir.
        if (file.EndsWith(".cshtml") && keys.Count > 0)
        {
            keys.AddRange(TitlePattern().Matches(source).Select(m => Unescape(m.Groups[1].Value)));
        }

        var missing = keys.Distinct().Where(k => !English.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "en.json'da karşılığı olmayan metinler: " + string.Join(" | ", missing));
    }

    [Fact]
    public void EnglishTexts_KeepTheSamePlaceholders()
    {
        var mismatched = English
            .Where(pair => !Placeholder().Matches(pair.Key).Select(m => m.Value).Order()
                .SequenceEqual(Placeholder().Matches(pair.Value).Select(m => m.Value).Order()))
            .Select(pair => pair.Key)
            .ToList();

        Assert.True(mismatched.Count == 0, "Yer tutucuları uyuşmayan çeviriler: " + string.Join(" | ", mismatched));
    }

    [Fact]
    public void SiteText_ReturnsTurkishOutsideEnglishRequests()
    {
        using (SiteCultures.Use(SiteCultures.Turkish))
        {
            Assert.Equal("Uzmanlar", SiteText.T("Uzmanlar"));
        }
    }

    private static string Unescape(string key) => key.Replace("\\\"", "\"").Replace("\\\\", "\\");

    private static string FindAppDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "bitirme.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("bitirme.sln bulunamadı."), "bitirme");
    }
}
