using System.Net;
using bitirme.Models;
using Microsoft.EntityFrameworkCore;

namespace bitirme.IntegrationTests;

/// <summary>Rol sınırları ve kayıt sahipliği: kim hangi sayfaya, kimin randevusuna ve notuna erişebilir.</summary>
public class AuthorizationTests(MoodicaFactory factory)
{
    private readonly TestData _data = new(factory);

    private async Task<AppClient> SignedInPatientAsync(Patient patient)
    {
        var client = factory.CreateAppClient();
        Assert.Equal("/Hasta/Dashboard", (await client.LoginPatientAsync(patient.Email!)).RedirectPath());
        return client;
    }

    private async Task<AppClient> SignedInDoctorAsync(Doctor doctor)
    {
        var client = factory.CreateAppClient();
        Assert.Equal("/Doktor/Dashboard", (await client.LoginDoctorAsync(doctor.Email!)).RedirectPath());
        return client;
    }

    [Theory]
    [InlineData("/Hasta/Dashboard", "/Hasta/Login")]
    [InlineData("/Appointment/Create", "/Hasta/Login")]
    [InlineData("/Hesap/IkiAdimliDogrulama", "/Hasta/Login")]
    [InlineData("/Doktor/Dashboard", "/Doktor/Login")]
    [InlineData("/Doktor/Profil", "/Doktor/Login")]
    public async Task Anonymous_ProtectedPage_RedirectsToSectionLogin_WithReturnUrl(string path, string loginPage)
    {
        var response = await factory.CreateAppClient().GetAsync(path);

        Assert.Equal($"{loginPage}?ReturnUrl={Uri.EscapeDataString(path)}", response.RedirectPath());
    }

    [Fact]
    public async Task Anonymous_AdminPanel_RedirectsToAdminLogin()
    {
        var response = await factory.CreateAppClient().GetAsync("/Yonetim");

        Assert.Equal("/Yonetim/Giris", response.RedirectPath());
    }

    [Theory]
    [InlineData("/Doktor/Dashboard")]
    [InlineData("/Doktor/Profil")]
    [InlineData("/Yonetim")]
    [InlineData("/Yonetim/Kullanicilar")]
    public async Task Patient_CannotOpenExpertOrAdminPages(string path)
    {
        var client = await SignedInPatientAsync(await _data.CreatePatientAsync());

        Assert.StartsWith("/Home/Index", (await client.GetAsync(path)).RedirectPath());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Hasta/Dashboard")).StatusCode);
    }

    [Theory]
    [InlineData("/Hasta/Dashboard")]
    [InlineData("/Appointment/Create")]
    [InlineData("/Yonetim")]
    public async Task Expert_CannotOpenPatientOrAdminPages(string path)
    {
        var client = await SignedInDoctorAsync(await _data.CreateDoctorAsync());

        Assert.StartsWith("/Home/Index", (await client.GetAsync(path)).RedirectPath());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Doktor/Dashboard")).StatusCode);
    }

    [Fact]
    public async Task MeetingRoom_OpenOnlyToAppointmentParties()
    {
        var patient = await _data.CreatePatientAsync();
        var doctor = await _data.CreateDoctorAsync();
        var appointment = await _data.CreateAppointmentAsync(patient, doctor, status: AppointmentStatus.Approved, startUtc: DateTime.UtcNow);
        var path = $"/Appointment/Meeting/{appointment.Id}";

        Assert.Equal(HttpStatusCode.OK, (await (await SignedInPatientAsync(patient)).GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await SignedInDoctorAsync(doctor)).GetAsync(path)).StatusCode);

        var otherPatient = await SignedInPatientAsync(await _data.CreatePatientAsync());
        var otherDoctor = await SignedInDoctorAsync(await _data.CreateDoctorAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherPatient.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDoctor.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task SessionNote_OnlyAppointmentsExpertCanWrite()
    {
        var patient = await _data.CreatePatientAsync();
        var doctor = await _data.CreateDoctorAsync();
        var appointment = await _data.CreateAppointmentAsync(patient, doctor);

        var otherDoctor = await SignedInDoctorAsync(await _data.CreateDoctorAsync());
        var denied = await otherDoctor.PostFormAsync("/Doktor/Dashboard", "/Appointment/UpdateNote",
            ("id", appointment.Id.ToString()), ("note", "Yetkisiz not"));
        Assert.Contains("\"success\":false", await denied.ReadTextAsync());

        var owner = await SignedInDoctorAsync(doctor);
        var allowed = await owner.PostFormAsync("/Doktor/Dashboard", "/Appointment/UpdateNote",
            ("id", appointment.Id.ToString()), ("note", "Seans notu"));
        Assert.Contains("\"success\":true", await allowed.ReadTextAsync());

        var savedNote = await _data.StoredNoteAsync(appointment.Id);
        Assert.Equal("Seans notu", savedNote);
    }
}
