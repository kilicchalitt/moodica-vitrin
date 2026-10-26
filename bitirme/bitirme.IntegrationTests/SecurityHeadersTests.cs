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

    /// <summary>
    /// ISSUE-53: betik, stil ve yazı tipleri sunucumuzdan gelir; ziyaretçinin IP'si CDN'lere/Google'a gitmez ve
    /// CDN'deki dosya değişse sayfaya giremez (CSP de dış kaynak tanımaz).
    /// </summary>
    [Fact]
    public async Task Pages_LoadNoThirdPartyScriptsStylesOrFonts()
    {
        var patient = await _data.CreatePatientAsync();
        var doctor = await _data.CreateDoctorAsync();
        var appointment = await _data.CreateAppointmentAsync(patient, doctor, status: AppointmentStatus.Approved, startUtc: DateTime.UtcNow);
        var token = TestContext.Current.CancellationToken;

        var anonymous = factory.CreateAppClient();
        var home = await anonymous.GetAsync("/");
        var patientClient = factory.CreateAppClient();
        await patientClient.LoginPatientAsync(patient.Email!);
        var pages = new[]
        {
            await home.Content.ReadAsStringAsync(token),
            await patientClient.Http.GetStringAsync("/Hasta/Dashboard", token),
            await patientClient.Http.GetStringAsync($"/Appointment/Meeting/{appointment.Id}", token),
        };

        var external = new System.Text.RegularExpressions.Regex("<(script|link)\\b[^>]*\\b(src|href)=\"(https?:)?//", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var html in pages)
        {
            Assert.DoesNotMatch(external, html);
            Assert.Contains("/lib/bootstrap/dist/css/bootstrap.min.css", html);
            Assert.Contains("/css/fonts.css", html);
        }

        var csp = Assert.Single(home.Headers.GetValues("Content-Security-Policy"));
        foreach (var directive in new[] { "script-src", "style-src", "font-src" })
        {
            Assert.DoesNotContain("https:", csp.Split(';').Single(d => d.Trim().StartsWith(directive, StringComparison.Ordinal)));
        }

        foreach (var file in new[]
                 {
                     "/lib/sweetalert2/sweetalert2.all.min.js", "/lib/microsoft-signalr/signalr.min.js",
                     "/lib/fontawesome/css/all.min.css", "/lib/fontawesome/webfonts/fa-solid-900.woff2",
                     "/lib/fonts/source-serif-4/source-serif-4-latin-ext-500-normal.woff2", "/lib/fonts/source-sans-3/source-sans-3-latin-ext-400-normal.woff2",
                 })
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, (await anonymous.GetAsync(file)).StatusCode);
        }
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
