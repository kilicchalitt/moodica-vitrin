using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace bitirme.Hubs
{
    public class NotificationHub : Hub
    {
        public static string PatientGroup(int patientId) => $"patient-{patientId}";
        public static string DoctorGroup(int doctorId) => $"doctor-{doctorId}";

        public override async Task OnConnectedAsync()
        {
            var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = Context.User?.FindFirstValue(ClaimTypes.Role);

            if (int.TryParse(userIdValue, out var userId))
            {
                if (role == "Patient")
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, PatientGroup(userId));
                }
                else if (role == "Doctor")
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, DoctorGroup(userId));
                }
            }

            await base.OnConnectedAsync();
        }
    }
}
