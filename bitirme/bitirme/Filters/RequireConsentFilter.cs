using System.Security.Claims;
using bitirme.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace bitirme.Filters
{
    /// <summary>
    /// Zorunlu aydınlatma/rıza metinlerinin güncel sürümünü onaylamamış danışan ve uzmanı onay sayfasına yönlendirir
    /// (TASK-32). Kayıttan önce açılmış hesaplar ve metin sürümü değiştiğinde herkes bu yoldan yeniden onaylar.
    /// Tüm denetleyicilere genel filtre olarak uygulanır; anonim eylemler ve <see cref="SkipConsentCheckAttribute"/>
    /// taşıyan eylemler etkilenmez. Yönetici bu akışa girmez.
    /// </summary>
    public sealed class RequireConsentFilter : IAsyncActionFilter
    {
        private readonly ConsentService _consents;

        public RequireConsentFilter(ConsentService consents)
        {
            _consents = consents;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            var metadata = context.ActionDescriptor.EndpointMetadata;
            var skip = metadata.OfType<IAllowAnonymous>().Any() || metadata.OfType<SkipConsentCheckAttribute>().Any();

            if (!skip && user.Identity?.IsAuthenticated == true &&
                ConsentCatalog.RoleFromName(user.FindFirstValue(ClaimTypes.Role)) is { } role &&
                int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                var missing = await _consents.GetMissingAsync(userId, role);
                if (missing.Count > 0)
                {
                    var request = context.HttpContext.Request;
                    var returnUrl = HttpMethods.IsGet(request.Method) ? request.Path + request.QueryString : null;
                    context.Result = new RedirectToActionResult("Onay", "Gizlilik", new { returnUrl });
                    return;
                }
            }

            await next();
        }
    }

    /// <summary>Rıza onayı olmadan da erişilebilmesi gereken eylemler (onay sayfasının kendisi, çıkış).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SkipConsentCheckAttribute : Attribute
    {
    }
}
