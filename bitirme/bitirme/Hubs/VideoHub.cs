using Microsoft.AspNetCore.SignalR;

namespace bitirme.Hubs
{
    public class VideoHub : Hub
    {
        // Bir kullanıcı (Doktor veya Hasta) odaya (randevu numarasına) katılır
        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            // Odaya diğer bağlananlara (kendisi hariç) birinin katıldığını haber ver
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("UserJoined", Context.ConnectionId);
        }

        // Aramayı başlatan taraf WebRTC "Offer" (Teklif) gönderir
        public async Task SendOffer(string offer, string roomId)
        {
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("ReceiveOffer", offer, Context.ConnectionId);
        }

        // Teklifi alan taraf kamerayı açıp WebRTC "Answer" (Cevap) gönderir
        public async Task SendAnswer(string answer, string roomId)
        {
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("ReceiveAnswer", answer);
        }

        // WebRTC P2P (Kişiden Kişiye) veri yolu bulma adresleri (ICE Candidate) gönderimi
        public async Task SendIceCandidate(string candidate, string roomId)
        {
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("ReceiveIceCandidate", candidate);
        }
        // Doktorun hastaya not gondermesi
        public async Task SendNote(string roomId, string note)
        {
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("ReceiveNote", note);
        }
        // Görüşmeden ayrılma
        public async Task LeaveRoom(string roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("UserLeft", Context.ConnectionId);
        }
    }
}