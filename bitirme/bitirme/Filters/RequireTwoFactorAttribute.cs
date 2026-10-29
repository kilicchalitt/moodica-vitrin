using bitirme.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using bitirme.Services.Localization;

namespace bitirme.Filters
{
    /// <summary>
    /// İki aşamalı doğrulaması açık olmayan oturumlu kullanıcıyı kurulum sayfasına yönlendirir.
    /// Anonim eylemler (ör. giriş sayfası) etkilenmez. Yönetim paneli için zorunludur (DECISION-19).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireTwoFactorAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var allowsAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
            if (!allowsAnonymous && context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
                var user = await userManager.GetUserAsync(context.HttpContext.User);
                if (user != null && !await userManager.GetTwoFactorEnabledAsync(user))
                {
                    var tempData = context.HttpContext.RequestServices
                        .GetRequiredService<ITempDataDictionaryFactory>()
                        .GetTempData(context.HttpContext);
                    tempData["AlertType"] = "warning";
                    tempData["AlertMessage"] = SiteText.T("Bu bölüm için önce iki aşamalı doğrulamayı kurmanız gerekiyor.");
                    context.Result = new RedirectToActionResult("IkiAdimliDogrulama", "Hesap", null);
                    return;
                }
            }

            await next();
        }
    }
}
