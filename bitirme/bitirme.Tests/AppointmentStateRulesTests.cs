using bitirme.Models;
using bitirme.Services;

namespace bitirme.Tests;

/// <summary>Randevu durum makinesi (DECISION-29, ISSUE-20).</summary>
public class AppointmentStateRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);
    private const string Reason = "Acil bir durum çıktı";

    private static string? Blocker(AppointmentStatus from, AppointmentStatus to, AppointmentActor actor, double hoursUntilStart, string? reason = Reason) =>
        AppointmentStateRules.GetBlocker(from, to, actor, Now.AddHours(hoursUntilStart), Now, reason);

    [Theory]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Approved)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Approved)]
    public void FinalStates_CannotChange(AppointmentStatus from, AppointmentStatus to)
    {
        Assert.Contains("değiştirilemez", Blocker(from, to, AppointmentActor.Doctor, 48));
    }

    [Fact]
    public void NothingGoesBackToPending()
    {
        Assert.NotNull(Blocker(AppointmentStatus.Approved, AppointmentStatus.Pending, AppointmentActor.Doctor, 48));
    }

    [Fact]
    public void OnlyDoctorApprovesLegacyPending()
    {
        Assert.Null(Blocker(AppointmentStatus.Pending, AppointmentStatus.Approved, AppointmentActor.Doctor, 48));
        Assert.NotNull(Blocker(AppointmentStatus.Pending, AppointmentStatus.Approved, AppointmentActor.Patient, 48));
        Assert.NotNull(Blocker(AppointmentStatus.Approved, AppointmentStatus.Approved, AppointmentActor.Doctor, 48));
    }

    [Theory]
    [InlineData(24.01, true)]
    [InlineData(24, false)]
    [InlineData(3, false)]
    public void PatientCancels_OnlyMoreThan24HoursBefore(double hoursUntilStart, bool allowed)
    {
        var blocker = Blocker(AppointmentStatus.Approved, AppointmentStatus.Cancelled, AppointmentActor.Patient, hoursUntilStart, reason: null);

        Assert.Equal(allowed, blocker == null);
    }

    [Fact]
    public void DoctorCancels_WithReason_UntilSessionEnds()
    {
        Assert.Null(Blocker(AppointmentStatus.Approved, AppointmentStatus.Cancelled, AppointmentActor.Doctor, 1));
        Assert.Contains("gerekçe", Blocker(AppointmentStatus.Approved, AppointmentStatus.Cancelled, AppointmentActor.Doctor, 1, reason: "  "));
        Assert.Contains("Bitmiş", Blocker(AppointmentStatus.Approved, AppointmentStatus.Cancelled, AppointmentActor.Doctor, -1));
        Assert.Contains("en fazla", Blocker(AppointmentStatus.Approved, AppointmentStatus.Cancelled, AppointmentActor.Doctor, 1,
            reason: new string('a', AppointmentStateRules.MaxCancellationReasonLength + 1)));
    }

    [Fact]
    public void SystemCancels_AnyActiveAppointment()
    {
        Assert.Null(Blocker(AppointmentStatus.Approved, AppointmentStatus.Cancelled, AppointmentActor.System, 0.5, reason: null));
    }

    [Fact]
    public void Completed_OnlyByDoctor_AfterSessionStarts()
    {
        Assert.Contains("başlamadan", Blocker(AppointmentStatus.Approved, AppointmentStatus.Completed, AppointmentActor.Doctor, 1));
        Assert.Null(Blocker(AppointmentStatus.Approved, AppointmentStatus.Completed, AppointmentActor.Doctor, -0.5));
        Assert.NotNull(Blocker(AppointmentStatus.Approved, AppointmentStatus.Completed, AppointmentActor.Patient, -0.5));
        Assert.NotNull(Blocker(AppointmentStatus.Pending, AppointmentStatus.Completed, AppointmentActor.Doctor, -0.5));
    }

    [Fact]
    public void PatientCanCancel_HelperMatchesRules()
    {
        var soon = new Appointment { Status = AppointmentStatus.Approved, AppointmentDate = Now.AddHours(5) };
        var later = new Appointment { Status = AppointmentStatus.Approved, AppointmentDate = Now.AddDays(3) };
        var cancelled = new Appointment { Status = AppointmentStatus.Cancelled, AppointmentDate = Now.AddDays(3) };

        Assert.False(AppointmentStateRules.PatientCanCancel(soon, Now));
        Assert.True(AppointmentStateRules.PatientCanCancel(later, Now));
        Assert.False(AppointmentStateRules.PatientCanCancel(cancelled, Now));
    }
}
