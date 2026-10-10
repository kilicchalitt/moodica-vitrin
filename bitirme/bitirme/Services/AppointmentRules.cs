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

        /// <summary>
        /// Saati dolduran durumlar: bekleyen, onaylı ve ödeme için ayrılmış (Faz 6). Veritabanı sorgularında kullanılır;
        /// tekil indekslerin süzgeci de bunlardır (AppDbContext).
        /// </summary>
        public static readonly AppointmentStatus[] SlotStatuses =
            [AppointmentStatus.Pending, AppointmentStatus.Approved, AppointmentStatus.AwaitingPayment];

        /// <summary>Bekleyen, onaylı ve ödeme bekleyen randevular saati doldurur; iptal ve tamamlananlar doldurmaz.</summary>
        public static bool OccupiesSlot(Appointment appointment) => OccupiesSlot(appointment.Status);

        public static bool OccupiesSlot(AppointmentStatus status) => SlotStatuses.Contains(status);

        /// <summary>
        /// Uzman, danışanın anket özetini yalnızca bu randevu üzerinden görebilir mi? Bekleyen, onaylı ve tamamlanan
        /// randevu erişim verir; iptal edilen (uzmanın reddi dahil) ve ödemesi tamamlanmamış randevu vermez — açık rıza
        /// "randevu alınan uzmanla paylaşım"dır.
        /// </summary>
        public static bool GrantsAssessmentAccess(AppointmentStatus status) =>
            status is not (AppointmentStatus.Cancelled or AppointmentStatus.AwaitingPayment);

        /// <summary>Verilen başlangıçtaki seans, saati dolduran mevcut randevulardan biriyle çakışıyor mu?</summary>
        public static bool HasConflict(DateTime start, IEnumerable<Appointment> existingAppointments) =>
            existingAppointments.Any(a => OccupiesSlot(a) && Overlaps(start, a.AppointmentDate));

        /// <summary>Görüşme odası seanstan bu kadar önce açılır (ISSUE-19).</summary>
        public static readonly TimeSpan MeetingOpensBefore = TimeSpan.FromMinutes(10);

        /// <summary>Odanın açıldığı an.</summary>
        public static DateTime MeetingOpensAt(DateTime start) => start - MeetingOpensBefore;

        /// <summary>Oda seans ve ardından gelen 10 dakikalık ara bitince (sonraki seansın başında) kapanır.</summary>
        public static DateTime MeetingClosesAt(DateTime start) => start.AddMinutes(SlotMinutes);

        /// <summary>Görüşme odasına şu an girilebilir mi; girilemiyorsa neden (ISSUE-19).</summary>
        public static MeetingAccess GetMeetingAccess(AppointmentStatus status, DateTime start, DateTime now)
        {
            if (status != AppointmentStatus.Approved)
            {
                return MeetingAccess.NotApproved;
            }

            if (now < MeetingOpensAt(start))
            {
                return MeetingAccess.TooEarly;
            }

            return now < MeetingClosesAt(start) ? MeetingAccess.Open : MeetingAccess.Ended;
        }

        /// <summary>Odaya girilemediğinde kullanıcıya gösterilen açıklama.</summary>
        public static string MeetingAccessMessage(MeetingAccess access) => access switch
        {
            MeetingAccess.NotApproved => "Görüşme odası yalnızca onaylı randevularda açılır.",
            MeetingAccess.TooEarly => $"Görüşme odası seanstan {MeetingOpensBefore.TotalMinutes:0} dakika önce açılır.",
            MeetingAccess.Ended => "Bu seansın görüşme süresi doldu.",
            _ => string.Empty
        };
    }

    /// <summary>Panellerde hangi randevuların görüneceği (Faz 6: ödeme akışı).</summary>
    public static class AppointmentVisibility
    {
        /// <summary>
        /// Uzman yalnızca ödemesi alınmış (ya da ödeme öncesinden kalan) randevuları görür; ödeme bekleyen ve hiç ödenmeden
        /// biten ayırmalar uzmana gösterilmez (danışanın şikayeti dahil).
        /// </summary>
        public static IQueryable<Appointment> VisibleToDoctor(this IQueryable<Appointment> appointments) =>
            appointments.Where(a => a.Payment == null || a.Payment.PaidAt != null);

        /// <summary>Danışan, ödenmeden biten (süresi dolan / vazgeçilen) ayırmaları görmez; ödeme bekleyeni görür.</summary>
        public static IQueryable<Appointment> VisibleToPatient(this IQueryable<Appointment> appointments) =>
            appointments.Where(a => a.Payment == null || a.Payment.PaidAt != null || a.Payment.Status == PaymentStatus.Pending);
    }

    /// <summary>Görüşme odasına erişim durumu.</summary>
    public enum MeetingAccess
    {
        Open,
        NotApproved,
        TooEarly,
        Ended
    }
}
