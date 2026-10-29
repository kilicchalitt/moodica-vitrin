using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Localization;

namespace bitirme.Services.Localization
{
    /// <summary>Türkçe/İngilizce desteğinin kurulumu (DECISION-49).</summary>
    public static class LocalizationSetup
    {
        public static IServiceCollection AddMoodicaLocalization(this IServiceCollection services)
        {
            services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
            services.AddLocalization();
            return services;
        }

        /// <summary>
        /// Uzun metinler (hukuki metinler, kendine yardım) için dil son ekli görünüm: <c>_AydinlatmaMetni.en.cshtml</c>.
        /// Form modellerindeki doğrulama mesajları ve alan adları da aynı sözlükten çevrilir.
        /// </summary>
        public static IMvcBuilder AddMoodicaViewLocalization(this IMvcBuilder mvc) =>
            mvc.AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix).AddDataAnnotationsLocalization();

        /// <summary>
        /// İstek dili yalnızca dil çerezinden gelir (yoksa Türkçe; tarayıcı dili kullanılmaz, birincil kitle Türkiye).
        /// Ardından gösterim saat dilimi <see cref="UserTimeZones.CookieName"/> çerezinden alınır.
        /// </summary>
        public static IApplicationBuilder UseMoodicaLocalization(this IApplicationBuilder app)
        {
            // Sözlük açılışta yüklenir: SiteText (denetleyici ve e-posta metinleri) ilk görünümden önce de çalışsın.
            app.ApplicationServices.GetRequiredService<IStringLocalizerFactory>();
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(SiteCultures.TurkishCulture),
                SupportedCultures = SiteCultures.Supported,
                SupportedUICultures = SiteCultures.Supported,
                RequestCultureProviders = [new CookieRequestCultureProvider()]
            });

            return app.Use(async (context, next) =>
            {
                var zoneId = context.Request.Cookies[UserTimeZones.CookieName];
                DisplayTimeZone.Set(UserTimeZones.IsValid(zoneId) ? UserTimeZones.Find(zoneId) : TurkeyTime.Zone);
                await next();
            });
        }

        /// <summary>Dil ve saat dilimi çerezlerini yazar (dil düğmesi, giriş, hesap ayarı). Bir yıl geçerli, yalnızca tercih bilgisi.</summary>
        public static void WritePreferenceCookies(this HttpResponse response, string? language, string? timeZoneId)
        {
            var options = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = response.HttpContext.Request.IsHttps,
                HttpOnly = false
            };
            if (language != null)
            {
                response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, SiteCultures.CookieValue(language), options);
            }

            if (UserTimeZones.IsValid(timeZoneId))
            {
                response.Cookies.Append(UserTimeZones.CookieName, timeZoneId!, options);
            }
        }
    }
}
