using System.Net;
using System.Text.Json;
using bitirme.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace bitirme.IntegrationTests;

/// <summary>
/// İstek sınırlama (Faz 8). Sınırlar ortak fabrikada kapalıdır; bu testler aynı veritabanıyla sınırları açık ikinci bir
/// uygulama başlatır. Test sunucusunda istemci adresi yoktur: bütün anonim istekler aynı "adres" sayılır.
/// </summary>
public class RateLimitTests(MoodicaFactory factory)
{
    private readonly TestData _data = new(factory);

    private AppClient LimitedClient(WebApplicationFactory<Program> app) => new(app.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    }));

    private WebApplicationFactory<Program> LimitedApp() =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:Enabled", "true"));

    [Fact]
    public async Task SignIn_IsLimitedPerAddress_WithTurkishPage()
    {
        await using var app = LimitedApp();
        var client = LimitedClient(app);

        for (var i = 0; i < 10; i++)
        {
            var attempt = await client.LoginPatientAsync("yok@example.com", "YanlisSifre1");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, attempt.StatusCode);
        }

        var limited = await client.LoginPatientAsync("yok@example.com", "YanlisSifre1");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Contains(RateLimitPolicies.RejectedMessage, await limited.ReadTextAsync());

        // Sınır yalnızca gönderimdedir: sayfalar açılmaya devam eder.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Hasta/Login")).StatusCode);
    }

    [Fact]
    public async Task Submission_IsLimitedPerUser_WithJsonErrors()
    {
        var patient = await _data.CreatePatientAsync();
        await using var app = LimitedApp();
        var client = LimitedClient(app);
        await client.LoginPatientAsync(patient.Email!);

        for (var i = 0; i < 10; i++)
        {
            var attempt = await client.PostJsonAsync("/Anket", "/Anket/Gonder", new { });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, attempt.StatusCode);
        }

        var limited = await client.PostJsonAsync("/Anket", "/Anket/Gonder", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using var json = JsonDocument.Parse(await limited.ReadTextAsync());
        Assert.Equal(RateLimitPolicies.RejectedMessage, json.RootElement.GetProperty("errors")[0].GetString());
    }
}
