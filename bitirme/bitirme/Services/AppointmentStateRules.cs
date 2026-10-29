using bitirme.Models;
using bitirme.Services.Localization;

namespace bitirme.Services
{
    /// <summary>
    /// Randevu durum makinesi (Faz 4, DECISION-29; ISSUE-20). Saf ve birim testlidir; zamanlar UTC.
    /// Bekleyen → onaylı / iptal; onaylı → iptal / tamamlandı; iptal ve tamamlandı son durumdur.
    /// </summary>
    public static class AppointmentStateRules
    {
        /// <summary>Danışan seansa bundan fazla varken panelden iptal edebilir.</summary>
        public static readonly TimeSpan PatientCancellationDeadline = TimeSpan.FromHours(24);

        public const int MinCancellationReasonLength = 5;
        public const int MaxCancellationReasonLength = 500;

        /// <summary>Danışan bu randevuyu şimdi iptal edebilir mi (panelde düğme gösterimi için)?</summary>
        public static bool PatientCanCancel(Appointment appointment, DateTime nowUtc) =>
            GetBlocker(appointment.Status, AppointmentStatus.Cancelled, AppointmentActor.Patient,
                appointment.AppointmentDate, nowUtc, reason: null) == null;

        /// <summary>Geçiş yapılamıyorsa nedeni (Türkçe), yapılabiliyorsa null.</summary>
        public static string? GetBlocker(
            AppointmentStatus from, AppointmentStatus to, AppointmentActor actor, DateTime startUtc, DateTime nowUtc, string? reason)
        {
            if (from is AppointmentStatus.Cancelled or AppointmentStatus.Completed)
            {
                return SiteText.T("İptal edilmiş veya tamamlanmış randevu değiştirilemez.");
            }

            if (from == to)
            {
                return "Randevu zaten bu durumda.";
            }

            // Ödeme bekleyen randevu yalnızca ödeme akışıyla onaylanır veya bırakılır (Faz 6; PaymentService).
            if (from == AppointmentStatus.AwaitingPayment)
            {
                return SiteText.T("Bu randevunun ödemesi bekleniyor.");
            }

            switch (to)
            {
                case AppointmentStatus.Pending:
                    return SiteText.T("Randevu beklemeye alınamaz.");

                case AppointmentStatus.Approved:
                    // Yeni randevular otomatik onaylıdır; yalnızca Faz 4 öncesinden kalan bekleyenleri uzman onaylar.
                    return actor == AppointmentActor.Doctor ? null : SiteText.T("Randevuyu yalnızca uzman onaylayabilir.");

                case AppointmentStatus.Completed:
                    if (actor != AppointmentActor.Doctor || from != AppointmentStatus.Approved)
                    {
                        return SiteText.T("Yalnızca uzman, onaylı bir randevuyu tamamlandı olarak işaretleyebilir.");
                    }

                    return nowUtc < startUtc ? SiteText.T("Seans başlamadan randevu tamamlandı olarak işaretlenemez.") : null;

                case AppointmentStatus.Cancelled:
                    return actor switch
                    {
                        AppointmentActor.Patient when startUtc - nowUtc <= PatientCancellationDeadline =>
                            SiteText.T("Seansa {0:0} saatten az kaldığı için randevu panelden iptal edilemez.", PatientCancellationDeadline.TotalHours),
                        AppointmentActor.Doctor when nowUtc >= AppointmentRules.EndOf(startUtc) =>
                            SiteText.T("Bitmiş bir seans iptal edilemez."),
                        AppointmentActor.Doctor when (reason?.Trim().Length ?? 0) < MinCancellationReasonLength =>
                            SiteText.T("Lütfen danışana iletilecek iptal gerekçesini yazın (en az {0} karakter).", MinCancellationReasonLength),
                        _ when reason?.Trim().Length > MaxCancellationReasonLength =>
                            SiteText.T("Gerekçe en fazla {0} karakter olabilir.", MaxCancellationReasonLength),
                        _ => null
                    };

                default:
                    return SiteText.T("Geçersiz durum.");
            }
        }
    }
}
