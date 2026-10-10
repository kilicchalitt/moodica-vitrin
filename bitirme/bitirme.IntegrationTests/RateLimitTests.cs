using System.Net;
using System.Text.Json;
using bitirme.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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

    // --- Ters vekil arkasında gerçek istemci adresi (Faz 9, ReverseProxySettings) ---

    private const string ProxyAddress = "10.0.0.5";

    /// <summary>Sınırları açık uygulama; bağlantı adresi <paramref name="connectionAddress"/> (test sunucusunda adres yoktur).</summary>
    private WebApplicationFactory<Program> ProxiedApp(bool proxyEnabled, string connectionAddress) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("ReverseProxy:Enabled", proxyEnabled ? "true" : "false");
            builder.UseSetting("ReverseProxy:KnownProxies:0", ProxyAddress);
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new ConnectionAddressFilter(IPAddress.Parse(connectionAddress))));
        });

    private static AppClient ClientFrom(WebApplicationFactory<Program> app, string forwardedFor)
    {
        var http = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        http.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return new AppClient(http);
    }

    private static async Task ExhaustSignInAsync(AppClient client)
    {
        for (var i = 0; i < 10; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.LoginPatientAsync("yok@example.com", "YanlisSifre1")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.LoginPatientAsync("yok@example.com", "YanlisSifre1")).StatusCode);
    }

    [Fact]
    public async Task BehindTrustedProxy_EachClientAddressHasItsOwnLimit()
    {
        await using var app = ProxiedApp(proxyEnabled: true, connectionAddress: ProxyAddress);

        await ExhaustSignInAsync(ClientFrom(app, "203.0.113.10"));

        var other = await ClientFrom(app, "203.0.113.11").LoginPatientAsync("yok@example.com", "YanlisSifre1");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, other.StatusCode);
    }

    [Fact]
    public async Task ForwardedHeader_IsIgnored_WhenProxyDisabled()
    {
        await using var app = ProxiedApp(proxyEnabled: false, connectionAddress: ProxyAddress);

        await ExhaustSignInAsync(ClientFrom(app, "203.0.113.20"));

        // Sahte başlıkla başka adres gibi görünmek sınırı aşmaya yetmez.
        var spoofed = await ClientFrom(app, "203.0.113.21").LoginPatientAsync("yok@example.com", "YanlisSifre1");
        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
    }

    [Fact]
    public async Task ForwardedHeader_IsIgnored_FromUntrustedAddress()
    {
        await using var app = ProxiedApp(proxyEnabled: true, connectionAddress: "198.51.100.7");

        await ExhaustSignInAsync(ClientFrom(app, "203.0.113.30"));

        var spoofed = await ClientFrom(app, "203.0.113.31").LoginPatientAsync("yok@example.com", "YanlisSifre1");
        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
    }

    /// <summary>Her isteğin bağlantı adresini verir (gerçek sunucuda vekilin adresi), uygulamanın ardışık düzeninden önce.</summary>
    private sealed class ConnectionAddressFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
