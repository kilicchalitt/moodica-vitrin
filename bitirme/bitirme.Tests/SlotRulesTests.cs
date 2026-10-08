using bitirme.Services;

namespace bitirme.Tests;

/// <summary>Boş saat hesabı (DECISION-29). Saatler Türkiye saatiyle kurulur, UTC karşılaştırılır.</summary>
public class SlotRulesTests
{
    // 5 Ekim 2026 Pazartesi 08:00 Türkiye saati.
    private static readonly DateTime Now = TurkeyTime.FromTurkey(new DateTime(2026, 10, 5, 8, 0, 0));

    private static DateTime Local(int day, int hour) => TurkeyTime.FromTurkey(new DateTime(2026, 10, day, hour, 0, 0));

    private static WorkingRange Range(DayOfWeek day, int start, int end) => new(day, new TimeOnly(start, 0), new TimeOnly(end, 0));

    private static IReadOnlyList<DateTime> Slots(
        IReadOnlyCollection<WorkingRange> hours, IReadOnlyCollection<LeaveRange>? leaves = null, IReadOnlyCollection<DateTime>? busy = null) =>
        SlotRules.Available(hours, leaves ?? [], busy ?? [], Now);

    [Fact]
    public void Range_ProducesHourlySlots_ThatFitBeforeEnd()
    {
        var slots = Slots([Range(DayOfWeek.Tuesday, 10, 13)]).Where(s => s < Local(7, 0)).ToList();

        Assert.Equal([Local(6, 10), Local(6, 11), Local(6, 12)], slots);
    }

    [Fact]
    public void SlotsBeforeLeadTime_AreExcluded()
    {
        // Şimdi 08:00; 3 saat kuralıyla bugünkü ilk saat 11:00.
        var today = Slots([Range(DayOfWeek.Monday, 9, 13)]).Where(s => s < Local(6, 0)).ToList();

        Assert.Equal([Local(5, 11), Local(5, 12)], today);
    }

    [Fact]
    public void SlotsBeyondThirtyDays_AreExcluded()
    {
        var slots = Slots(WorkingHoursRules.Default.ToList());

        Assert.All(slots, s => Assert.True(s <= Now + AppointmentRules.BookingHorizon));
        Assert.Contains(Local(30, 10), slots);         // 30 Ekim Cuma
        Assert.DoesNotContain(Local(5, 10), slots);    // bugün 3 saatten yakın
        Assert.True(slots.Count > 150);
    }

    [Fact]
    public void Leave_RemovesOverlappingSlots()
    {
        var leave = new LeaveRange(Local(6, 10).AddMinutes(30), Local(6, 12));

        var slots = Slots([Range(DayOfWeek.Tuesday, 10, 13)], [leave]).Where(s => s < Local(7, 0)).ToList();

        Assert.Equal([Local(6, 12)], slots);
    }

    [Fact]
    public void BusyAppointments_RemoveOverlappingSlots_IncludingLegacyHalfHourStarts()
    {
        var slots = Slots([Range(DayOfWeek.Tuesday, 10, 14)], busy: [Local(6, 10), Local(6, 12).AddMinutes(30)])
            .Where(s => s < Local(7, 0)).ToList();

        Assert.Equal([Local(6, 11)], slots);
    }

    [Fact]
    public void Weekends_OnlyWhenExpertWorks()
    {
        Assert.DoesNotContain(Slots(WorkingHoursRules.Default.ToList()), s => s.ToTurkey().DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Contains(Local(10, 10), Slots([Range(DayOfWeek.Saturday, 10, 12)]));
    }

    [Fact]
    public void NoWorkingHours_NoSlots()
    {
        Assert.Empty(Slots([]));
    }
}
