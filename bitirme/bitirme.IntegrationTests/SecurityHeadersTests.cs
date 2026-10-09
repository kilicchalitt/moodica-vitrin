using bitirme.Models;

namespace bitirme.IntegrationTests;

/// <summary>Tarayıcı tarafı güvenlik (Faz 8): satır içi olay işleyicisi olmaması ve kullanıcı metninin betiğe girmemesi.</summary>
public class SecurityHeadersTests(MoodicaFactory factory)
{
    private readonly TestData _data = new(factory);

    [Fact]
    public async Task Pages_SendSecurityHeaders_AndInlineScriptsCarryRequestNonce()
    {
        var client = factory.CreateAppClient();
        await client.LoginPatientAsync((await _data.CreatePatientAsync()).Email!);
        var first = await client.GetAsync("/Hasta/Dashboard");
        var second = await client.GetAsync("/Hasta/Dashboard");

        var csp = Assert.Single(first.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        var scriptSrc = csp.Split(';').Single(d => d.Trim().StartsWith("script-src", StringComparison.Ordinal));
        Assert.DoesNotContain("unsafe-inline", scriptSrc);
        Assert.DoesNotContain("unsafe-eval", scriptSrc);
        Assert.DoesNotContain("localhost", csp); // geliştirme kaynakları yalnızca Development ortamında
        Assert.Equal("DENY", Assert.Single(first.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("nosniff", Assert.Single(first.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(first.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("camera=(self)", Assert.Single(first.Headers.GetValues("Permissions-Policy")));

        // Satır içi betikler yalnızca bu yanıtın nonce'uyla çalışır; nonce her istekte değişir.
        var nonce = System.Text.RegularExpressions.Regex.Match(csp, "'nonce-([^']+)'").Groups[1].Value;
        Assert.NotEmpty(nonce);
        var html = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains($"<script nonce=\"{nonce}\">", html);
        Assert.DoesNotContain($"'nonce-{nonce}'", Assert.Single(second.Headers.GetValues("Content-Security-Policy")));
    }

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
