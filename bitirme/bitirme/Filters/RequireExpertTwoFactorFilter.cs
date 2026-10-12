using bitirme.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;

namespace bitirme.Filters
{
    /// <summary>Uzmanlar için iki aşamalı doğrulama zorunluluğu (DECISION-21: kapalı betadan önce açılır).</summary>
    public sealed class TwoFactorPolicyOptions
    {
        public const string SectionName = "TwoFactor";

        /// <summary>Açıkken 2FA'sı olmayan uzman, uzman sayfalarına girmeden önce kurulum sayfasına yönlenir. Varsayılan kapalı.</summary>
        public bool RequireForExperts { get; set; }
    }

    /// <summary>
    /// Genel filtre: <see cref="TwoFactorPolicyOptions.RequireForExperts"/> açıkken, 2FA'sı kapalı uzman uzman panelini, takvimi ve
    /// randevu işlemlerini (Doktor, Takvim, Appointment) kullanamaz; kurulum sayfasına yönlenir. Kurulum, çıkış, gizlilik metinleri,
    /// Verilerim ve herkese açık sayfalar etkilenmez (kendi hesabını ayarlayabilmesi ve KVKK haklarını kullanabilmesi için).
    /// </summary>
    public sealed class RequireExpertTwoFactorFilter(IOptionsMonitor<TwoFactorPolicyOptions> options) : IAsyncActionFilter
    {
        public const string Message = "Uzman hesapları için iki aşamalı doğrulama zorunludur. Devam etmek için lütfen kurun.";

        private static readonly HashSet<string> ExpertControllers = new(StringComparer.OrdinalIgnoreCase) { "Doktor", "Takvim", "Appointment" };

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            if (options.CurrentValue.RequireForExperts &&
                user.Identity?.IsAuthenticated == true &&
                user.IsInRole(AppRoles.Doctor) &&
                context.ActionDescriptor is ControllerActionDescriptor action &&
                ExpertControllers.Contains(action.ControllerName) &&
                !context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
                var stored = await userManager.GetUserAsync(user);
                if (stored != null && !await userManager.GetTwoFactorEnabledAsync(stored))
                {
                    var tempData = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(context.HttpContext);
                    tempData["AlertType"] = "warning";
                    tempData["AlertMessage"] = Message;
                    context.Result = new RedirectToActionResult("IkiAdimliDogrulama", "Hesap", null);
                    return;
                }
            }

            await next();
        }
    }
}
