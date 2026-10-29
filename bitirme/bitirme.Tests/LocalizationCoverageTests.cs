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

    [GeneratedRegex("""(?:(?:\bL)\[|\bL\.GetString\(|SiteText\.(?:T|Key)\()\s*"((?:[^"\\]|\\.)*)"(?=\s*[\],)])""")]
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
    public void FormModelMessages_HaveAnEnglishVersion()
    {
        // Doğrulama mesajları ve alan adları AddDataAnnotationsLocalization ile sözlükten çevrilir.
        var texts = typeof(SiteText).Assembly.GetTypes()
            .SelectMany(t => t.GetProperties())
            .SelectMany(p => p.GetCustomAttributes(inherit: true))
            .SelectMany(a => a switch
            {
                System.ComponentModel.DataAnnotations.ValidationAttribute v when v.ErrorMessage != null => [v.ErrorMessage],
                System.ComponentModel.DataAnnotations.DisplayAttribute d when d.Name != null => [d.Name],
                _ => Array.Empty<string>()
            })
            .Distinct();

        var missing = texts.Where(t => !English.ContainsKey(t)).ToList();
        Assert.True(missing.Count == 0, "en.json'da karşılığı olmayan form metinleri: " + string.Join(" | ", missing));
    }

    [Theory]
    [InlineData("Views/Gizlilik", "_*Metni.cshtml")]
    [InlineData("Views/KendineYardim/Icerik", "_*.cshtml")]
    public void LongTexts_HaveAnEnglishView(string folder, string pattern)
    {
        // Hukuki metinler ve kendine yardım yazıları çeviri sözlüğünde değil, _X.en.cshtml görünümünde (I-9).
        var turkish = Directory.GetFiles(Path.Combine(AppDirectory, folder), pattern)
            .Where(f => !f.EndsWith(".en.cshtml"))
            .ToList();

        Assert.NotEmpty(turkish);
        var missing = turkish.Where(f => !File.Exists(f[..^".cshtml".Length] + ".en.cshtml")).Select(Path.GetFileName).ToList();
        Assert.True(missing.Count == 0, "İngilizce görünümü olmayan metinler: " + string.Join(" | ", missing));
    }

    [Fact]
    public void StoredCancellationReasons_HaveAnEnglishVersion()
    {
        // Sistemin yazdığı iptal gerekçeleri Türkçe saklanır, danışan panelinde SiteText.T ile çevrilir.
        string[] reasons =
        [
            bitirme.Services.AppointmentStatusService.SystemCancellationReason,
            bitirme.Services.AppointmentStatusService.HealthConsentWithdrawnReason,
            bitirme.Services.DoctorScheduleService.LeaveCancellationReason,
            bitirme.Services.AccountDeletionService.PatientDeletedReason,
            bitirme.Services.Payments.PaymentService.HoldExpiredReason,
            bitirme.Services.Payments.PaymentService.AbandonedReason
        ];

        Assert.All(reasons, r => Assert.True(English.ContainsKey(r), "en.json'da yok: " + r));
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
