using Microsoft.AspNetCore.SignalR;
using MyMvcApp.Services;

namespace MyMvcApp.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ChatService _chat;
        public ChatHub(ChatService chat)
        {
            _chat = chat;
        }

        public async Task SendMessage(string message)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", message, true);

            var botReply = await _chat.ProcessMessageAsync(Context.ConnectionId, message);

            await Clients.Caller.SendAsync("ReceiveMessage", botReply, false);
        }
    }
}
