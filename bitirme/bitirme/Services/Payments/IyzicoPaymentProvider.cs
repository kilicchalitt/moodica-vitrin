using System.Globalization;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Options;

namespace bitirme.Services.Payments
{
    /// <summary>
    /// iyzico ayarları (<c>Payment:Iyzico</c>). Anahtarlar User Secrets / ortam değişkeninde (DECISION-08). Sandbox
    /// anahtarları "sandbox-" ile başlar ve sandbox adresiyle çalışır; yayında sandbox adresi kabul edilmez.
    /// </summary>
    public sealed class IyzicoOptions
    {
        public const string SectionName = "Payment:Iyzico";
        public const string ProviderName = "Iyzico";
        public const string SandboxBaseUrl = "https://sandbox-api.iyzipay.com";
        public const string LiveBaseUrl = "https://api.iyzipay.com";

        public string? ApiKey { get; set; }
        public string? SecretKey { get; set; }
        public string BaseUrl { get; set; } = SandboxBaseUrl;

        /// <summary>
        /// iyzico alıcı için TC kimlik ve adres alanlarını zorunlu tutar; Moodica bunları toplamaz (DECISION-17). Canlıdan önce
        /// avukat ve sağlayıcıyla netleşir (HUKUK.md H-09); o zamana dek bu varsayılanlar gönderilir.
        /// </summary>
        public string DefaultIdentityNumber { get; set; } = "11111111111";
        public string DefaultCity { get; set; } = "Istanbul";
        public string DefaultCountry { get; set; } = "Turkey";
        public string DefaultAddress { get; set; } = "Online hizmet";

        public bool IsSandbox => BaseUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase);

        /// <summary>Açılışta denetlenir: hatalı ayarla uygulama başlamaz.</summary>
        public string? GetConfigurationError(bool isProduction)
        {
            if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(SecretKey))
            {
                return "Payment:Provider=Iyzico için Payment:Iyzico:ApiKey ve Payment:Iyzico:SecretKey verilmeli.";
            }

            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return "Payment:Iyzico:BaseUrl geçerli bir https adresi olmalı.";
            }

            if (isProduction && IsSandbox)
            {
                return "Yayında (Production) iyzico sandbox adresi kullanılamaz; Payment:Iyzico:BaseUrl canlı adres olmalı.";
            }

            return null;
        }
    }

    /// <summary>
    /// iyzico Checkout Form bağdaştırıcısı (Faz 6 Aşama B; DECISION-44). Danışan kartını iyzico'nun barındırılan ödeme
    /// sayfasında girer (kart bilgisi Moodica'ya gelmez); iyzico tarayıcıyı dönüş adresine "token" ile POST eder; sonuç
    /// sunucudan iyzico'ya sorulur ve ödeme kaydı ile tutarla karşılaştırılır. İade, ödeme kalemi (paymentTransactionId)
    /// üzerinden yapılır; sağlayıcı ödeme numarası "paymentId/paymentTransactionId" olarak saklanır. Anahtar ve kart
    /// bilgisi loga yazılmaz; yalnızca iyzico'nun hata kodu ve mesajı.
    /// </summary>
    public sealed class IyzicoPaymentProvider(IOptions<IyzicoOptions> options, ILogger<IyzicoPaymentProvider> logger) : IPaymentProvider
    {
        private readonly IyzicoOptions _options = options.Value;

        public string Name => IyzicoOptions.ProviderName;

        public bool IsAvailable => true;

        private Iyzipay.Options Client => new()
        {
            ApiKey = _options.ApiKey,
            SecretKey = _options.SecretKey,
            BaseUrl = _options.BaseUrl
        };

        public async Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken cancellationToken = default)
        {
            // iyzico'nun .NET paketi yalnızca eşzamanlı çağrı sunar; istek iş parçacığı beklemesin diye arka planda çalışır.
            var form = await Task.Run(() => CheckoutFormInitialize.Create(IyzicoMapping.ToCheckoutRequest(request, _options), Client), cancellationToken);
            if (!IyzicoMapping.IsSuccess(form.Status) || string.IsNullOrEmpty(form.Token) || string.IsNullOrEmpty(form.PaymentPageUrl))
            {
                logger.LogWarning("iyzico ödeme formu açılamadı (ödeme {PaymentId}): {Code} {Message}", request.PaymentId, form.ErrorCode, form.ErrorMessage);
                return new PaymentStartResult(null, null, "Ödeme sayfası açılamadı.");
            }

            return new PaymentStartResult(form.Token, form.PaymentPageUrl, null);
        }

        public async Task<PaymentVerifyResult> VerifyAsync(PaymentVerifyRequest request, CancellationToken cancellationToken = default)
        {
            var form = await Task.Run(() => CheckoutForm.Retrieve(new RetrieveCheckoutFormRequest
            {
                Locale = Locale.TR.ToString(),
                ConversationId = request.PaymentId.ToString(CultureInfo.InvariantCulture),
                Token = request.Token
            }, Client), cancellationToken);

            var result = IyzicoMapping.ToVerifyResult(request, form.Status, form.PaymentStatus, form.BasketId, form.PaidPrice,
                form.PaymentId, form.PaymentItems?.Select(i => i.PaymentTransactionId).ToList(), form.ErrorMessage);
            if (!result.Succeeded)
            {
                logger.LogWarning("iyzico ödemesi doğrulanmadı (ödeme {PaymentId}): {Code} {Error}", request.PaymentId, form.ErrorCode, result.Error);
            }

            return result;
        }

        public async Task<RefundResult> RefundAsync(string providerPaymentId, decimal amount, CancellationToken cancellationToken = default)
        {
            if (IyzicoMapping.ParseProviderPaymentId(providerPaymentId) is not { } ids)
            {
                return new RefundResult(null, "iyzico ödeme numarası okunamadı.");
            }

            var refund = await Task.Run(() => Refund.Create(new CreateRefundRequest
            {
                Locale = Locale.TR.ToString(),
                ConversationId = ids.PaymentId,
                PaymentTransactionId = ids.TransactionId,
                Price = IyzicoMapping.FormatPrice(amount),
                Currency = Currency.TRY.ToString()
            }, Client), cancellationToken);

            if (!IyzicoMapping.IsSuccess(refund.Status))
            {
                logger.LogWarning("iyzico iadesi yapılamadı ({PaymentId}): {Code} {Message}", ids.PaymentId, refund.ErrorCode, refund.ErrorMessage);
                return new RefundResult(null, refund.ErrorMessage ?? "iyzico iadeyi reddetti.");
            }

            return new RefundResult($"{refund.PaymentId}/{refund.PaymentTransactionId}", null);
        }
    }

    /// <summary>iyzico istek/yanıt eşlemesi (saf; birim testli, ağ gerektirmez).</summary>
    public static class IyzicoMapping
    {
        public const string BasketItemName = "Moodica online seans";

        public static bool IsSuccess(string? status) => string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        /// <summary>iyzico fiyatı nokta ayraçlı metin ister ("1100.0").</summary>
        public static string FormatPrice(decimal amount) => amount.ToString("0.0#", CultureInfo.InvariantCulture);

        public static CreateCheckoutFormInitializeRequest ToCheckoutRequest(PaymentStartRequest request, IyzicoOptions options)
        {
            var id = request.PaymentId.ToString(CultureInfo.InvariantCulture);
            var price = FormatPrice(request.Amount);
            var buyer = request.Buyer;
            var fullName = $"{buyer.FirstName} {buyer.LastName}".Trim();
            var address = new Address
            {
                ContactName = fullName,
                City = options.DefaultCity,
                Country = options.DefaultCountry,
                Description = options.DefaultAddress
            };

            return new CreateCheckoutFormInitializeRequest
            {
                Locale = Locale.TR.ToString(),
                ConversationId = id,
                Price = price,
                PaidPrice = price,
                Currency = Currency.TRY.ToString(),
                BasketId = id,
                PaymentGroup = PaymentGroup.PRODUCT.ToString(),
                CallbackUrl = request.CallbackUrl,
                EnabledInstallments = [1],
                Buyer = new Buyer
                {
                    Id = buyer.Id.ToString(CultureInfo.InvariantCulture),
                    Name = buyer.FirstName,
                    Surname = buyer.LastName,
                    Email = buyer.Email,
                    GsmNumber = ToInternationalPhone(buyer.Phone),
                    IdentityNumber = options.DefaultIdentityNumber,
                    RegistrationAddress = options.DefaultAddress,
                    City = options.DefaultCity,
                    Country = options.DefaultCountry,
                    Ip = string.IsNullOrWhiteSpace(buyer.Ip) ? "127.0.0.1" : buyer.Ip
                },
                BillingAddress = address,
                ShippingAddress = address,
                BasketItems =
                [
                    new BasketItem
                    {
                        Id = id,
                        // Veri en aza indirme: ödeme kuruluşuna uzmanın adı ve seans saati gitmez (VERI_KORUMA.md).
                        Name = BasketItemName,
                        Category1 = "Online terapi seansı",
                        ItemType = BasketItemType.VIRTUAL.ToString(),
                        Price = price
                    }
                ]
            };
        }

        /// <summary>
        /// Ödeme ancak iyzico "başarılı" der, sepet numarası bu ödeme kaydına aitse ve ödenen tutar beklenene eşitse kabul edilir
        /// (başka bir ödemenin belirteciyle ya da değiştirilmiş tutarla randevu açılamasın).
        /// </summary>
        public static PaymentVerifyResult ToVerifyResult(PaymentVerifyRequest request, string? status, string? paymentStatus,
            string? basketId, string? paidPrice, string? paymentId, IReadOnlyList<string>? transactionIds, string? errorMessage)
        {
            if (!IsSuccess(status) || !string.Equals(paymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                return new PaymentVerifyResult(null, string.IsNullOrWhiteSpace(errorMessage) ? "Ödeme tamamlanmadı." : errorMessage);
            }

            if (basketId != request.PaymentId.ToString(CultureInfo.InvariantCulture))
            {
                return new PaymentVerifyResult(null, "Ödeme bu randevuya ait değil.");
            }

            if (!decimal.TryParse(paidPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var paid) || paid != request.Amount)
            {
                return new PaymentVerifyResult(null, "Ödenen tutar beklenenle uyuşmuyor.");
            }

            if (string.IsNullOrEmpty(paymentId) || transactionIds is not [var transactionId] || string.IsNullOrEmpty(transactionId))
            {
                return new PaymentVerifyResult(null, "iyzico ödeme ayrıntısı eksik.");
            }

            return new PaymentVerifyResult($"{paymentId}/{transactionId}", null);
        }

        public static (string PaymentId, string TransactionId)? ParseProviderPaymentId(string? value) =>
            value?.Split('/') is [{ Length: > 0 } paymentId, { Length: > 0 } transactionId] ? (paymentId, transactionId) : null;

        /// <summary>Numara E.164 ("+905XXXXXXXXX") saklanır, olduğu gibi gönderilir; boşsa null.</summary>
        public static string? ToInternationalPhone(string? phone) =>
            phone is { Length: > 1 } && phone[0] == '+' ? phone : null;
    }
}
