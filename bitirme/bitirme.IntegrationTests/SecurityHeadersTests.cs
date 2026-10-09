using bitirme.Models;

namespace bitirme.IntegrationTests;

/// <summary>Tarayıcı tarafı güvenlik (Faz 8): satır içi olay işleyicisi olmaması ve kullanıcı metninin betiğe girmemesi.</summary>
public class SecurityHeadersTests(MoodicaFactory factory)
{
    private readonly TestData _data = new(factory);

    [Fact]
    public async Task ExpertPages_HaveNoInlineHandlers_AndPatientNameStaysData()
    {
        var patient = await _data.CreatePatientAsync(firstName: "Ay'şe\");alert(1);//");
        var doctor = await _data.CreateDoctorAsync();
        var appointment = await _data.CreateAppointmentAsync(patient, doctor, status: AppointmentStatus.Approved, startUtc: DateTime.UtcNow);

        var expert = factory.CreateAppClient();
        await expert.LoginDoctorAsync(doctor.Email!);
        var dashboard = await expert.Http.GetStringAsync("/Doktor/Dashboard", TestContext.Current.CancellationToken);
        var meeting = await expert.Http.GetStringAsync($"/Appointment/Meeting/{appointment.Id}", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("onclick=", dashboard + meeting, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"data-select-patient=\"{patient.Id}\"", dashboard);
        Assert.Contains("data-patient-name=\"Ay&#x27;", dashboard);
        Assert.DoesNotContain("alert(1)\"", dashboard);
    }
}
