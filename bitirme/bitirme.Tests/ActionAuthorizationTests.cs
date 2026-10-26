using System.Reflection;
using bitirme.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bitirme.Tests;

/// <summary>
/// "Yetkisiz uç nokta yok" kuralı (docs/README.md §3): herkese açık eylemler yalnızca bu listedekilerdir. Yeni bir eylem
/// yetkisiz kalırsa (sınıfta da eylemde de [Authorize] yoksa) test düşer (iç güvenlik testi, 10 Ekim 2026).
/// </summary>
public class ActionAuthorizationTests
{
    private static readonly HashSet<string> PublicActions =
    [
        "Dil.Degistir",
        "Doktor.Login", "Doktor.Register", "Doktor.Fotograf",
        "Gizlilik.Aydinlatma", "Gizlilik.AcikRiza", "Gizlilik.UzmanAydinlatma",
        "Gizlilik.KullaniciSozlesmesi", "Gizlilik.UzmanSozlesmesi",
        "Hasta.Login", "Hasta.Register",
        "Hesap.EpostaDogrula", "Hesap.SifremiUnuttum", "Hesap.SifreSifirla", "Hesap.IkiAdimliGiris",
        "Hesap.KurtarmaTalebi", "Hesap.KurtarmaTalebiGonder", "Hesap.KurtarmaTalebiOnayla",
        "Home.Index", "Home.NasilCalisir", "Home.Logout", "Home.Error",
        "KendineYardim.Index", "KendineYardim.Icerik",
        "Odeme.Sonuc", "Odeme.TestSaglayici",
        "Uzmanlar.Index", "Uzmanlar.Profil",
        "Yonetim.Giris",
    ];

    private static IEnumerable<(string Name, bool IsPublic)> Actions()
    {
        var controllers = typeof(HomeController).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Controller).IsAssignableFrom(t));
        foreach (var controller in controllers)
        {
            var classAuthorized = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null);
            foreach (var action in actions)
            {
                var isPublic = action.GetCustomAttribute<AllowAnonymousAttribute>() != null ||
                               (!classAuthorized && action.GetCustomAttribute<AuthorizeAttribute>() == null);
                yield return ($"{controller.Name[..^"Controller".Length]}.{action.Name}", isPublic);
            }
        }
    }

    [Fact]
    public void OnlyListedActions_AreReachableWithoutSignIn()
    {
        var unexpected = Actions().Where(a => a.IsPublic && !PublicActions.Contains(a.Name)).Select(a => a.Name).Distinct().Order();
        Assert.Empty(unexpected);
    }

    [Fact]
    public void PublicActionList_HasNoStaleEntries()
    {
        var existing = Actions().Where(a => a.IsPublic).Select(a => a.Name).ToHashSet();
        Assert.Empty(PublicActions.Where(name => !existing.Contains(name)).Order());
    }
}
