using bitirme.Services.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.SignalR;

namespace bitirme.Hubs
{
    /// <summary>
    /// Hub yöntemleri istek ara katmanının dışında çalışır; bağlantıyı açan isteğin dili (dil çerezi) her çağrıda yeniden
    /// kurulur. Böylece arayana dönen hata metinleri (HubException) onun dilinde olur (DECISION-49).
    /// </summary>
    public sealed class HubCultureFilter : IHubFilter
    {
        public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            using (SiteCultures.Use(LanguageOf(invocationContext.Context)))
            {
                return await next(invocationContext);
            }
        }

        private static string LanguageOf(HubCallerContext context) =>
            context.GetHttpContext()?.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture.TwoLetterISOLanguageName
            ?? SiteCultures.Turkish;
    }
}
