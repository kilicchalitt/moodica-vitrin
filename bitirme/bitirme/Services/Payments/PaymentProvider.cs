using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace bitirme.Services.Payments
{
    /// <summary>
    /// Ödeme kuruluşuna giden alıcı bilgisi (dolandırıcılık denetimi ve makbuz için; aydınlatma metninde "ödeme kuruluşuna
    /// aktarım"). TC kimlik ve adres toplanmaz (DECISION-17); sağlayıcı zorunlu tutuyorsa bağdaştırıcı ayardaki varsayılanı
    /// gönderir (HUKUK.md H-09).
    /// </summary>
    public sealed record PaymentBuyer(int Id, string FirstName, string LastName, string Email, string? Phone, string? Ip);

    /// <summary>Ödeme başlatma isteği: tutar, açıklama, alıcı ve sağlayıcının tarayıcıyı geri göndereceği adres.</summary>
    public sealed record PaymentStartRequest(int PaymentId, decimal Amount, string Description, string CallbackUrl, PaymentBuyer Buyer);

    /// <summary>Doğrulama: sağlayıcının bildirdiği ödeme bu ödeme kaydına ve bu tutara ait olmalı.</summary>
    public sealed record PaymentVerifyRequest(string Token, int PaymentId, decimal Amount);

    /// <summary>Başarıda belirteç ve danışanın yönlendirileceği ödeme sayfası adresi.</summary>
    public sealed record PaymentStartResult(string? Token, string? RedirectUrl, string? Error)
    {
        public bool Succeeded => Error == null;
    }

    /// <summary>Sağlayıcıdan doğrulanan ödeme sonucu; başarıda sağlayıcının ödeme numarası.</summary>
    public sealed record PaymentVerifyResult(string? ProviderPaymentId, string? Error)
    {
        public bool Succeeded => Error == null;
    }

    public sealed record RefundResult(string? ProviderRefundId, string? Error)
    {
        public bool Succeeded => Error == null;
    }

    /// <summary>
    /// Sağlayıcıdan bağımsız ödeme arayüzü (Faz 6 Aşama A). Akış barındırılan ödeme sayfası biçimindedir (iyzico Checkout
    /// Form gibi): <see cref="StartAsync"/> sayfa adresini verir, danışan kartını sağlayıcının sayfasında girer (kart bilgisi
    /// Moodica'ya hiç gelmez), sağlayıcı tarayıcıyı dönüş adresine gönderir, sonuç <see cref="VerifyAsync"/> ile sunucudan
    /// sağlayıcıya sorulur (tarayıcının söylediğine güvenilmez). Bağdaştırıcılar: sahte (geliştirme/test) ve iyzico.
    /// </summary>
    public interface IPaymentProvider
    {
        string Name { get; }

        /// <summary>Ödeme alınabiliyor mu (yayında sağlayıcı ayarlı mı).</summary>
        bool IsAvailable { get; }

        Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken cancellationToken = default);

        Task<PaymentVerifyResult> VerifyAsync(PaymentVerifyRequest request, CancellationToken cancellationToken = default);

        /// <summary>Ödemenin <paramref name="amount"/> kadarını iade eder (tam iade: ödenen tutar).</summary>
        Task<RefundResult> RefundAsync(string providerPaymentId, decimal amount, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Sahte (test) sağlayıcı: yalnızca geliştirme ve testte. Ödeme sayfası Moodica içindeki "test ödeme sayfası"dır
    /// (<c>/Odeme/TestSaglayici</c>); orada "başarılı" veya "başarısız" seçilir, gerçek kart bilgisi istenmez ve alınmaz.
    /// Sonuçlar bellekte tutulur (uygulama yeniden başlayınca yarım kalan test ödemeleri doğrulanamaz).
    /// </summary>
    public sealed class FakePaymentProvider : IPaymentProvider
    {
        public const string TestPagePath = "/Odeme/TestSaglayici";

        private readonly ConcurrentDictionary<string, FakeCheckout> _checkouts = new();
        private readonly ConcurrentDictionary<string, decimal> _refunds = new();

        public string Name => PaymentOptions.FakeProvider;

        public bool IsAvailable => true;

        private readonly ConcurrentDictionary<string, bool> _failingRefunds = new();

        /// <summary>Testler için: bu ödemenin iadesi başarısız olsun (sağlayıcı hatasını taklit eder; testler paralel koşar).</summary>
        public void SetRefundFailure(string providerPaymentId, bool fail)
        {
            if (fail)
            {
                _failingRefunds[providerPaymentId] = true;
            }
            else
            {
                _failingRefunds.TryRemove(providerPaymentId, out _);
            }
        }

        /// <summary>Yapılan iadeler (sağlayıcı ödeme numarası → tutar); testler doğrular.</summary>
        public IReadOnlyDictionary<string, decimal> Refunds => _refunds;

        public Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken cancellationToken = default)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            _checkouts[token] = new FakeCheckout(request.Amount, request.Description, request.CallbackUrl);
            return Task.FromResult(new PaymentStartResult(token, $"{TestPagePath}?token={token}", null));
        }

        /// <summary>Test ödeme sayfasının gösterdiği bilgiler; belirteç bilinmiyorsa null.</summary>
        public FakeCheckout? Find(string? token) => token != null && _checkouts.TryGetValue(token, out var checkout) ? checkout : null;

        /// <summary>Test ödeme sayfasında seçilen sonuç (bir kez; sonra değişmez).</summary>
        public bool Complete(string token, bool success)
        {
            if (!_checkouts.TryGetValue(token, out var checkout) || checkout.Succeeded != null)
            {
                return false;
            }

            _checkouts[token] = checkout with { Succeeded = success };
            return true;
        }

        public Task<PaymentVerifyResult> VerifyAsync(PaymentVerifyRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Find(request.Token) switch
            {
                { } checkout when checkout.Amount != request.Amount => new PaymentVerifyResult(null, "Ödenen tutar beklenenle uyuşmuyor."),
                { Succeeded: true } => new PaymentVerifyResult("fake-" + request.Token, null),
                { Succeeded: false } => new PaymentVerifyResult(null, "Kart reddedildi (test ödemesi)."),
                _ => new PaymentVerifyResult(null, "Ödeme tamamlanmadı.")
            });

        public Task<RefundResult> RefundAsync(string providerPaymentId, decimal amount, CancellationToken cancellationToken = default)
        {
            if (_failingRefunds.ContainsKey(providerPaymentId))
            {
                return Task.FromResult(new RefundResult(null, "Sağlayıcı iadeyi reddetti (test)."));
            }

            _refunds[providerPaymentId] = amount;
            return Task.FromResult(new RefundResult("fake-refund-" + providerPaymentId, null));
        }
    }

    public sealed record FakeCheckout(decimal Amount, string Description, string CallbackUrl, bool? Succeeded = null);

    /// <summary>Yayında sağlayıcı ayarlı değil: ödeme alınamaz, randevu açılmaz.</summary>
    public sealed class UnavailablePaymentProvider : IPaymentProvider
    {
        public string Name => "Yok";

        public bool IsAvailable => false;

        public Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentStartResult(null, null, "Ödeme sağlayıcısı ayarlı değil."));

        public Task<PaymentVerifyResult> VerifyAsync(PaymentVerifyRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentVerifyResult(null, "Ödeme sağlayıcısı ayarlı değil."));

        public Task<RefundResult> RefundAsync(string providerPaymentId, decimal amount, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RefundResult(null, "Ödeme sağlayıcısı ayarlı değil."));
    }

    public static class PaymentServiceCollectionExtensions
    {
        /// <summary>
        /// Sağlayıcı seçimi (<c>Payment:Provider</c>): "Iyzico" → iyzico (anahtarlar zorunlu); boş/"Fake" → geliştirme ve testte
        /// sahte sağlayıcı (gerçek para hareketi yok). Yayında sahte sağlayıcı açılışta reddedilir (bedava randevu açılmasın);
        /// sağlayıcı seçilmemişse yayında ödeme kapalıdır.
        /// </summary>
        public static IServiceCollection AddMoodicaPayments(this IServiceCollection services, IConfiguration configuration,
            IHostEnvironment environment)
        {
            services.AddOptions<PaymentOptions>()
                .Bind(configuration.GetSection(PaymentOptions.SectionName))
                .Validate(options => options.IsValid(), "Payment ayarları geçersiz (ücret sınırları, platform payı 0–1, saat ayırma 5–60 dk).")
                .ValidateOnStart();

            var provider = configuration[$"{PaymentOptions.SectionName}:Provider"];
            var isTestEnvironment = environment.IsDevelopment() || environment.IsEnvironment("Testing");
            if (string.Equals(provider, PaymentOptions.FakeProvider, StringComparison.OrdinalIgnoreCase) && !isTestEnvironment)
            {
                throw new InvalidOperationException("Sahte ödeme sağlayıcısı (Payment:Provider=Fake) yalnızca geliştirme ve testte kullanılabilir.");
            }

            if (string.Equals(provider, IyzicoOptions.ProviderName, StringComparison.OrdinalIgnoreCase))
            {
                // Anahtarlar User Secrets / ortam değişkeninde (DECISION-08). Yayında sandbox adresi kabul edilmez.
                var iyzico = configuration.GetSection(IyzicoOptions.SectionName).Get<IyzicoOptions>() ?? new IyzicoOptions();
                if (iyzico.GetConfigurationError(environment.IsProduction()) is { } error)
                {
                    throw new InvalidOperationException(error);
                }

                services.Configure<IyzicoOptions>(configuration.GetSection(IyzicoOptions.SectionName));
                services.AddSingleton<IPaymentProvider, IyzicoPaymentProvider>();
            }
            else if (isTestEnvironment && (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, PaymentOptions.FakeProvider, StringComparison.OrdinalIgnoreCase)))
            {
                services.AddSingleton<FakePaymentProvider>();
                services.AddSingleton<IPaymentProvider>(sp => sp.GetRequiredService<FakePaymentProvider>());
            }
            else
            {
                services.AddSingleton<IPaymentProvider, UnavailablePaymentProvider>();
            }

            services.AddScoped<PaymentService>();
            services.AddScoped<PaymentReport>();
            services.AddHostedService<PaymentBackgroundWorker>();
            return services;
        }
    }
}
