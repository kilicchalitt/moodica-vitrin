namespace bitirme.Services
{
    /// <summary>Uzmanın bir izni (UTC).</summary>
    public readonly record struct LeaveRange(DateTime Start, DateTime End);

    /// <summary>
    /// Boş saatler (DECISION-29): uzmanın haftalık çalışma saatlerinden (Türkiye saati) saat başı 50 dakikalık seanslar
    /// üretilir; takvim aralığı dışındakiler, izinle ve dolu randevularla çakışanlar çıkarılır. Saf ve birim testlidir.
    /// </summary>
    public static class SlotRules
    {
        /// <param name="hours">Uzmanın haftalık çalışma aralıkları (Türkiye saati).</param>
        /// <param name="leaves">Uzmanın izinleri (UTC).</param>
        /// <param name="busyStarts">Saati dolduran randevuların başlangıçları (UTC): uzmanınkiler ve randevuyu alacak danışanınkiler.</param>
        /// <param name="nowUtc">Şimdi (UTC).</param>
        /// <returns>Boş seans başlangıçları (UTC), artan sırada.</returns>
        public static IReadOnlyList<DateTime> Available(
            IReadOnlyCollection<WorkingRange> hours,
            IReadOnlyCollection<LeaveRange> leaves,
            IReadOnlyCollection<DateTime> busyStarts,
            DateTime nowUtc)
        {
            var slots = new List<DateTime>();
            var firstDay = nowUtc.ToTurkey().Date;
            var lastDay = (nowUtc + AppointmentRules.BookingHorizon).ToTurkey().Date;

            for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
            {
                foreach (var range in hours.Where(h => h.Day == day.DayOfWeek).OrderBy(h => h.Start))
                {
                    // Aralığın sonuna sığan her saat başı bir seans başlangıcıdır (10:00–13:00 → 10, 11, 12).
                    for (var local = day.Add(range.Start.ToTimeSpan());
                         local.AddMinutes(AppointmentRules.SessionMinutes) <= day.Add(range.End.ToTimeSpan());
                         local = local.AddMinutes(AppointmentRules.SlotMinutes))
                    {
                        var start = TurkeyTime.FromTurkey(local);
                        var end = AppointmentRules.EndOf(start);
                        if (AppointmentRules.IsWithinBookingWindow(start, nowUtc) &&
                            !leaves.Any(l => start < l.End && end > l.Start) &&
                            !busyStarts.Any(b => AppointmentRules.Overlaps(start, b)))
                        {
                            slots.Add(start);
                        }
                    }
                }
            }

            return slots.Distinct().Order().ToList();
        }
    }
}
