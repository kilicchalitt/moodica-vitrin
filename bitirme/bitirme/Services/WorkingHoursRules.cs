using bitirme.Models;
using bitirme.Services.Localization;

namespace bitirme.Services
{
    /// <summary>Bir çalışma aralığı (Türkiye saati).</summary>
    public readonly record struct WorkingRange(DayOfWeek Day, TimeOnly Start, TimeOnly End);

    /// <summary>
    /// Uzmanın haftalık çalışma saatleri kuralları (DECISION-29). Seanslar saat başında başladığı için aralıklar tam
    /// saattir. Veritabanından bağımsızdır ve birim testlerle doğrulanır.
    /// </summary>
    public static class WorkingHoursRules
    {
        public static readonly TimeOnly EarliestStart = new(7, 0);
        public static readonly TimeOnly LatestEnd = new(23, 0);
        public const int MaxRangesPerDay = 3;

        /// <summary>Pazartesiden başlayan gün sırası (arayüz ve listeleme için).</summary>
        public static readonly IReadOnlyList<DayOfWeek> Week =
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        ];

        /// <summary>Faz 4 öncesinden kalan ve yeni kaydolan uzmanların varsayılanı: hafta içi 10:00–20:00 (eski kural).</summary>
        public static IReadOnlyList<WorkingRange> Default { get; } = Week
            .Where(d => d is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(d => new WorkingRange(d, new TimeOnly(10, 0), new TimeOnly(20, 0)))
            .ToList();

        public static string DayName(DayOfWeek day) => Localization.SiteCultures.CurrentCulture.DateTimeFormat.GetDayName(day);

        /// <summary>Kaydetmeden önce denetim; hata yoksa boş liste. Hiç aralık olmaması geçerlidir (uzman randevu almaz).</summary>
        public static IReadOnlyList<string> Validate(IReadOnlyCollection<WorkingRange> ranges)
        {
            var errors = new List<string>();

            foreach (var range in ranges)
            {
                var label = DayName(range.Day);
                if (range.Start.Minute != 0 || range.End.Minute != 0 || range.Start.Second != 0 || range.End.Second != 0)
                {
                    errors.Add(SiteText.T("{0}: saatler tam saat olmalı (seanslar saat başında başlar).", label));
                }
                else if (range.Start >= range.End)
                {
                    errors.Add(SiteText.T("{0}: bitiş saati başlangıçtan sonra olmalı.", label));
                }
                else if (range.Start < EarliestStart || range.End > LatestEnd)
                {
                    errors.Add(SiteText.T("{0}: çalışma saatleri {1:HH\\:mm}–{2:HH\\:mm} arasında olmalı.", label, EarliestStart, LatestEnd));
                }
            }

            foreach (var day in ranges.GroupBy(r => r.Day))
            {
                var ordered = day.OrderBy(r => r.Start).ToList();
                if (ordered.Count > MaxRangesPerDay)
                {
                    errors.Add(SiteText.T("{0}: en fazla {1} aralık girilebilir.", DayName(day.Key), MaxRangesPerDay));
                }

                if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.Second.Start < pair.First.End))
                {
                    errors.Add(SiteText.T("{0}: aralıklar çakışıyor.", DayName(day.Key)));
                }
            }

            return errors.Distinct().ToList();
        }

        /// <summary>Yeni uzman nesnesine eklenecek varsayılan saatler (uzmanla birlikte kaydedilir).</summary>
        public static List<DoctorWorkingHours> DefaultEntities() => ToEntities(0, Default);

        public static List<DoctorWorkingHours> ToEntities(int doctorId, IEnumerable<WorkingRange> ranges) => ranges
            .Select(r => new DoctorWorkingHours { DoctorId = doctorId, Day = r.Day, Start = r.Start, End = r.End })
            .ToList();
    }
}
