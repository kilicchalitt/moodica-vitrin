using System;
using System.Collections.Generic;
using System.Linq;
using bitirme.Models;

namespace bitirme.Services
{
    /// <summary>
    /// Randevu oluşturma kuralları. Veritabanından bağımsızdır ve birim testlerle doğrulanır.
    /// </summary>
    public static class AppointmentRules
    {
        /// <summary>Her seans 30 dakika sürer.</summary>
        public const int SessionMinutes = 30;

        /// <summary>Randevu alınabilen ilk saat (dahil).</summary>
        public const int FirstHour = 10;

        /// <summary>Randevu alınabilen son saat sınırı (hariç).</summary>
        public const int LastHourExclusive = 20;

        public static bool IsWeekend(DateTime start) =>
            start.DayOfWeek == DayOfWeek.Saturday || start.DayOfWeek == DayOfWeek.Sunday;

        public static bool IsWithinWorkingHours(DateTime start) =>
            start.Hour >= FirstHour && start.Hour < LastHourExclusive;

        /// <summary>Bekleyen ve onaylı randevular saati doldurur; iptal ve tamamlananlar doldurmaz.</summary>
        public static bool OccupiesSlot(Appointment appointment) =>
            appointment.Status == AppointmentStatus.Pending || appointment.Status == AppointmentStatus.Approved;

        /// <summary>Verilen başlangıçtaki seans, mevcut randevulardan biriyle çakışıyor mu?</summary>
        public static bool HasConflict(DateTime start, IEnumerable<Appointment> existingAppointments)
        {
            var end = start.AddMinutes(SessionMinutes);
            return existingAppointments.Any(a =>
                OccupiesSlot(a) &&
                start < a.AppointmentDate.AddMinutes(SessionMinutes) &&
                end > a.AppointmentDate);
        }
    }
}
