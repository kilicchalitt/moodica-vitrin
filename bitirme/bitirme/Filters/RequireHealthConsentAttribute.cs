using System.Security.Claims;
using bitirme.Models;
using bitirme.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace bitirme.Filters
{
    /// <summary>
    /// Sağlık verisi toplayan işlemler (anket, randevu, ilerleme ölçümü, geri bildirim): sağlık verisi açık rızasını geri almış
    /// danışan "Verilerim" sayfasına yönlenir (DECISION-35). Danışan rızayı orada yeniden verebilir. Diğer roller etkilenmez.
    /// </summary>
    public sealed class RequireHealthConsentAttribute : TypeFilterAttribute
    {
        public const string BlockedMessage =
            "Sağlık verisi açık rızanızı geri aldığınız için bu işlem yapılamıyor. Dilerseniz Verilerim sayfasından rızanızı yeniden verebilirsiniz.";

        public RequireHealthConsentAttribute() : base(typeof(Filter))
        {
        }

        private sealed class Filter(ConsentService consents, ITempDataDictionaryFactory tempData) : IAsyncActionFilter
        {
            public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            {
                var user = context.HttpContext.User;
                if (user.IsInRole(AppRoles.Patient) &&
                    int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var patientId) &&
                    await consents.HasWithdrawnHealthConsentAsync(patientId))
                {
                    var request = context.HttpContext.Request;
                    if (request.HasJsonContentType() || request.Headers.Accept.ToString().Contains("application/json"))
                    {
                        context.Result = new ConflictObjectResult(new { success = false, message = BlockedMessage, errors = new[] { BlockedMessage } });
                        return;
                    }

                    tempData.GetTempData(context.HttpContext)["AlertMessage"] = BlockedMessage;
                    context.Result = new RedirectToActionResult("Index", "Verilerim", null);
                    return;
                }

                await next();
            }
        }
    }
}
