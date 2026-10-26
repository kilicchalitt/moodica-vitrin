namespace bitirme.Services.Localization
{
    /// <summary>
    /// Kullanıcının saatleri gördüğü saat dilimi (DECISION-49). Veritabanı UTC, randevu kuralları ve uzmanın çalışma saatleri
    /// Türkiye saatidir (<see cref="TurkeyTime"/>); bu sınıf yalnızca gösterimi kişinin saatine çevirir. Seçim
    /// <see cref="CookieName"/> çerezinde tutulur (tarayıcının saat dilimiyle ilk ziyarette dolar; oturum açınca hesaptaki
    /// <see cref="Models.User.TimeZoneId"/> yazılır), e-postalar alıcının hesabındaki dilimle hazırlanır.
    /// </summary>
    public static class UserTimeZones
    {
        public const string Default = "Europe/Istanbul";
        public const string CookieName = "moodica-tz";

        /// <summary>Seçim listesindeki saat dilimleri (IANA kimliği, Türkçe ve İngilizce ad). Tarayıcıdan gelen başka geçerli bir dilim de kabul edilir.</summary>
        public static readonly IReadOnlyList<(string Id, string Turkish, string English)> Common =
        [
            ("Europe/Istanbul", "Türkiye", "Turkey"),
            ("Europe/London", "Birleşik Krallık (Londra)", "United Kingdom (London)"),
            ("Europe/Dublin", "İrlanda (Dublin)", "Ireland (Dublin)"),
            ("Europe/Lisbon", "Portekiz (Lizbon)", "Portugal (Lisbon)"),
            ("Europe/Berlin", "Almanya (Berlin)", "Germany (Berlin)"),
            ("Europe/Amsterdam", "Hollanda (Amsterdam)", "Netherlands (Amsterdam)"),
            ("Europe/Brussels", "Belçika (Brüksel)", "Belgium (Brussels)"),
            ("Europe/Paris", "Fransa (Paris)", "France (Paris)"),
            ("Europe/Zurich", "İsviçre (Zürih)", "Switzerland (Zurich)"),
            ("Europe/Vienna", "Avusturya (Viyana)", "Austria (Vienna)"),
            ("Europe/Rome", "İtalya (Roma)", "Italy (Rome)"),
            ("Europe/Madrid", "İspanya (Madrid)", "Spain (Madrid)"),
            ("Europe/Stockholm", "İsveç (Stockholm)", "Sweden (Stockholm)"),
            ("Europe/Athens", "Yunanistan (Atina)", "Greece (Athens)"),
            ("Asia/Nicosia", "Kıbrıs (Lefkoşa)", "Cyprus (Nicosia)"),
            ("Europe/Kyiv", "Ukrayna (Kiev)", "Ukraine (Kyiv)"),
            ("Europe/Moscow", "Rusya (Moskova)", "Russia (Moscow)"),
            ("Asia/Baku", "Azerbaycan (Bakü)", "Azerbaijan (Baku)"),
            ("Asia/Dubai", "Birleşik Arap Emirlikleri (Dubai)", "United Arab Emirates (Dubai)"),
            ("Asia/Riyadh", "Suudi Arabistan (Riyad)", "Saudi Arabia (Riyadh)"),
            ("Asia/Qatar", "Katar (Doha)", "Qatar (Doha)"),
            ("America/New_York", "ABD Doğu (New York)", "US Eastern (New York)"),
            ("America/Chicago", "ABD Orta (Chicago)", "US Central (Chicago)"),
            ("America/Denver", "ABD Dağ (Denver)", "US Mountain (Denver)"),
            ("America/Los_Angeles", "ABD Batı (Los Angeles)", "US Pacific (Los Angeles)"),
            ("America/Toronto", "Kanada (Toronto)", "Canada (Toronto)"),
            ("Australia/Sydney", "Avustralya (Sidney)", "Australia (Sydney)")
        ];

        /// <summary>Geçerli IANA saat dilimi kimliği mi?</summary>
        public static bool IsValid(string? id) =>
            !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && TryFind(id) != null;

        /// <summary>Kimliğe karşılık gelen saat dilimi; geçersizse Türkiye.</summary>
        public static TimeZoneInfo Find(string? id) => (id != null ? TryFind(id) : null) ?? TurkeyTime.Zone;

        /// <summary>Listede adı varsa adı, yoksa kimliğin kendisi ("Europe/London" → "Birleşik Krallık (Londra)").</summary>
        public static string DisplayName(string? id)
        {
            var match = Common.FirstOrDefault(z => z.Id == id);
            return match.Id == null ? id ?? Default : SiteCultures.IsEnglish ? match.English : match.Turkish;
        }

        /// <summary>Verilen andaki UTC farkı: "GMT+3", "GMT+0", "GMT-5", "GMT+5:30".</summary>
        public static string Offset(TimeZoneInfo zone, DateTime utc)
        {
            var offset = zone.GetUtcOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            var abs = offset.Duration();
            return abs.Minutes == 0 ? $"GMT{sign}{abs.Hours}" : $"GMT{sign}{abs.Hours}:{abs.Minutes:00}";
        }

        private static TimeZoneInfo? TryFind(string id)
        {
            if (id == Default)
            {
                return TurkeyTime.Zone;
            }

            return TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;
        }
    }

    /// <summary>İsteğin gösterim saat dilimi; ara katman çerezden doldurur, e-posta hazırlanırken alıcınınkiyle değiştirilir.</summary>
    public static class DisplayTimeZone
    {
        private static readonly AsyncLocal<TimeZoneInfo?> CurrentZone = new();

        public static TimeZoneInfo Current => CurrentZone.Value ?? TurkeyTime.Zone;

        /// <summary>Türkiye saatinden farklı bir dilimde mi gösteriliyor?</summary>
        public static bool IsNotTurkey => Current.BaseUtcOffset != TurkeyTime.Zone.BaseUtcOffset || Current.SupportsDaylightSavingTime;

        public static void Set(TimeZoneInfo zone) => CurrentZone.Value = zone;

        /// <summary>Kod bloğu süresince gösterim dilimini değiştirir.</summary>
        public static IDisposable Use(string? timeZoneId)
        {
            var previous = CurrentZone.Value;
            CurrentZone.Value = UserTimeZones.Find(timeZoneId);
            return new Restore(() => CurrentZone.Value = previous);
        }

        private sealed class Restore(Action restore) : IDisposable
        {
            public void Dispose() => restore();
        }
    }
}
