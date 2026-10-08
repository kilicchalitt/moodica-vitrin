namespace bitirme.Services.Email
{
    /// <summary>Hatırlatma türü.</summary>
    public enum ReminderKind
    {
        DayBefore,
        HourBefore
    }

    /// <summary>
    /// Giden kutusu ve hatırlatma kuralları (ISSUE-25, DECISION-30). Saf ve birim testlidir; zamanlar UTC.
    /// </summary>
    public static class EmailRules
    {
        public const int MaxAttempts = 5;
        public const int BatchSize = 20;

        /// <summary>Gönderilen veya vazgeçilen e-postaların içeriği bu süre sonra silinir (kişisel veri).</summary>
        public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

        /// <summary>Başarısız denemeden sonra bekleme: 1, 2, 4, 8 … dakika, en fazla 60.</summary>
        public static TimeSpan RetryDelay(int attempts) =>
            TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, attempts - 1))));

        /// <summary>
        /// Seansa kalan süreye göre gönderilmesi gereken hatırlatma: 23–24 saat kala "yarın", son 1 saatte "birazdan".
        /// Pencere dışındaysa null (servis kapalıyken kaçan hatırlatma sonradan gönderilmez).
        /// </summary>
        public static ReminderKind? DueReminder(DateTime startUtc, DateTime nowUtc, bool dayBeforeSent, bool hourBeforeSent)
        {
            var remaining = startUtc - nowUtc;
            if (!dayBeforeSent && remaining > TimeSpan.FromHours(23) && remaining <= TimeSpan.FromHours(24))
            {
                return ReminderKind.DayBefore;
            }

            if (!hourBeforeSent && remaining > TimeSpan.Zero && remaining <= TimeSpan.FromHours(1))
            {
                return ReminderKind.HourBefore;
            }

            return null;
        }
    }
}
