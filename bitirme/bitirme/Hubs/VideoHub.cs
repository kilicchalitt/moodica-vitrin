using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using bitirme.Data;
using bitirme.Models;
using bitirme.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace bitirme.Hubs
{
    /// <summary>
    /// Görüntülü seans için WebRTC sinyalleşmesi.
    /// Yalnızca giriş yapmış kullanıcılar bağlanabilir ve bir odaya (randevuya)
    /// yalnızca o randevunun hastası veya uzmanı katılabilir.
    /// </summary>
    [Authorize]
    public class VideoHub : Hub
    {
        private const string JoinedRoomsKey = "JoinedRooms";
        private readonly AppDbContext _context;

        private readonly TimeProvider _time;

        public VideoHub(AppDbContext context, TimeProvider time)
        {
            _context = context;
            _time = time;
        }

        // Bir kullanıcı (Doktor veya Hasta) odaya (randevu numarasına) katılır
        public async Task JoinRoom(string roomId)
        {
            if (!int.TryParse(roomId, out var appointmentId))
            {
                throw new HubException("Geçersiz görüşme odası.");
            }

            var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = Context.User?.FindFirstValue(ClaimTypes.Role);
            if (!int.TryParse(userIdValue, out var userId))
            {
                throw new HubException("Bu görüşmeye erişim yetkiniz yok.");
            }

            var appointment = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.Id == appointmentId &&
                            ((role == AppRoles.Patient && a.PatientId == userId) ||
                             (role == AppRoles.Doctor && a.DoctorId == userId)))
                .Select(a => new { a.Status, a.AppointmentDate })
                .FirstOrDefaultAsync();

            // Onayı geri alınmış veya askıdaki uzman görüşmeye katılamaz (ISSUE-38).
            if (appointment == null || (role == AppRoles.Doctor &&
                !await _context.Doctors.Visible(_time.GetUtcNow()).AnyAsync(d => d.Id == userId)))
            {
                throw new HubException("Bu görüşmeye erişim yetkiniz yok.");
            }

            // Oda yalnızca onaylı randevuda ve seans saatine yakın açılır (ISSUE-19).
            var access = AppointmentRules.GetMeetingAccess(appointment.Status, appointment.AppointmentDate, _time.UtcNow());
            if (access != MeetingAccess.Open)
            {
                throw new HubException(AppointmentRules.MeetingAccessMessage(access));
            }

            GetJoinedRooms().Add(roomId);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            // Odaya diğer bağlananlara (kendisi hariç) birinin katıldığını haber ver
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("UserJoined", Context.ConnectionId);
        }

        // Aramayı başlatan taraf WebRTC "Offer" (Teklif) gönderir
        public async Task SendOffer(string offer, string roomId)
        {
            EnsureJoined(roomId);
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("ReceiveOffer", offer, Context.ConnectionId);
        }

        // Teklifi alan taraf kamerayı açıp WebRTC "Answer" (Cevap) gönderir
        public async Task SendAnswer(string answer, string roomId)
        {
            EnsureJoined(roomId);
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("ReceiveAnswer", answer);
        }

        // WebRTC P2P (Kişiden Kişiye) veri yolu bulma adresleri (ICE Candidate) gönderimi
        public async Task SendIceCandidate(string candidate, string roomId)
        {
            EnsureJoined(roomId);
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("ReceiveIceCandidate", candidate);
        }

        // Doktorun hastaya not gondermesi (yalnızca doktor)
        public async Task SendNote(string roomId, string note)
        {
            EnsureJoined(roomId);
            if (Context.User?.FindFirstValue(ClaimTypes.Role) != AppRoles.Doctor)
            {
                throw new HubException("Not gönderme yetkiniz yok.");
            }

            if (note?.Length > AppointmentRules.MaxNoteLength)
            {
                throw new HubException(AppointmentRules.NoteTooLongMessage);
            }

            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("ReceiveNote", note);
        }

        // Görüşmeden ayrılma
        public async Task LeaveRoom(string roomId)
        {
            if (!GetJoinedRooms().Remove(roomId))
            {
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("UserLeft", Context.ConnectionId);
        }

        // Sekme kapanır veya bağlantı koparsa karşı taraf "ayrıldı" bildirimini alır (ISSUE-11).
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            foreach (var roomId in GetJoinedRooms())
            {
                await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("UserLeft", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private HashSet<string> GetJoinedRooms()
        {
            if (Context.Items.TryGetValue(JoinedRoomsKey, out var value) && value is HashSet<string> rooms)
            {
                return rooms;
            }

            rooms = new HashSet<string>();
            Context.Items[JoinedRoomsKey] = rooms;
            return rooms;
        }

        private void EnsureJoined(string roomId)
        {
            if (!GetJoinedRooms().Contains(roomId))
            {
                throw new HubException("Bu görüşme odasına katılmadınız.");
            }
        }
    }
}
