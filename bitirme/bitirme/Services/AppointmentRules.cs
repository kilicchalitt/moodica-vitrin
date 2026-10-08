using System;
using System.Collections.Generic;
using System.Linq;
using bitirme.Models;

namespace bitirme.Services
{
    /// <summary>
    /// Randevu kuralları (DECISION-29). Veritabanından bağımsızdır ve birim testlerle doğrulanır. Zamanlar UTC'dir.
    /// </summary>
    public static class AppointmentRules
    {
        /// <summary>Bir seans 50 dakika sürer; ardından 10 dakika ara (seanslar saat başında başlar).</summary>
        public const int SessionMinutes = 50;

        /// <summary>Seans başlangıçları arasındaki adım (saat başı).</summary>
        public const int SlotMinutes = 60;

        /// <summary>Randevu en erken bu kadar sonrası için alınabilir.</summary>
        public static readonly TimeSpan MinLeadTime = TimeSpan.FromHours(3);

        /// <summary>Randevu en geç bu kadar sonrası için alınabilir.</summary>
        public static readonly TimeSpan BookingHorizon = TimeSpan.FromDays(30);

        /// <summary>Danışanın randevu şikayeti/notu için en fazla karakter (ISSUE-40).</summary>
        public const int MaxComplaintLength = 1000;

        /// <summary>Uzmanın seans notu için en fazla karakter (ISSUE-40).</summary>
        public const int MaxNoteLength = 4000;

        public static string NoteTooLongMessage => $"Not en fazla {MaxNoteLength} karakter olabilir.";

        /// <summary>Seansın bittiği an.</summary>
        public static DateTime EndOf(DateTime start) => start.AddMinutes(SessionMinutes);

        /// <summary>İki seans zaman olarak çakışıyor mu?</summary>
        public static bool Overlaps(DateTime start, DateTime otherStart) =>
            start < EndOf(otherStart) && otherStart < EndOf(start);

        /// <summary>Takvim aralığında mı: en erken <see cref="MinLeadTime"/>, en geç <see cref="BookingHorizon"/> sonrası.</summary>
        public static bool IsWithinBookingWindow(DateTime start, DateTime now) =>
            start >= now + MinLeadTime && start <= now + BookingHorizon;

        /// <summary>Bekleyen ve onaylı randevular saati doldurur; iptal ve tamamlananlar doldurmaz.</summary>
        public static bool OccupiesSlot(Appointment appointment) => OccupiesSlot(appointment.Status);

        public static bool OccupiesSlot(AppointmentStatus status) =>
            status == AppointmentStatus.Pending || status == AppointmentStatus.Approved;

        /// <summary>
        /// Uzman, danışanın anket özetini yalnızca bu randevu üzerinden görebilir mi? Bekleyen, onaylı ve tamamlanan
        /// randevu erişim verir; iptal edilen (uzmanın reddi dahil) vermez — açık rıza "randevu alınan uzmanla paylaşım"dır.
        /// </summary>
        public static bool GrantsAssessmentAccess(AppointmentStatus status) => status != AppointmentStatus.Cancelled;

        /// <summary>Verilen başlangıçtaki seans, saati dolduran mevcut randevulardan biriyle çakışıyor mu?</summary>
        public static bool HasConflict(DateTime start, IEnumerable<Appointment> existingAppointments) =>
            existingAppointments.Any(a => OccupiesSlot(a) && Overlaps(start, a.AppointmentDate));
    }
}
