using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MyMvcApp.Hubs;
using MyMvcApp.Models;
using MyMvcApp.Services;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyMvcApp.Controllers
{
    public class ChatController : Controller
    {
        private readonly ChatService _processor;
        private readonly ChatFormatter _formatter;
        private readonly IHubContext<ChatHub> _hub;

        public ChatController(
            ChatService processor,
            ChatFormatter formatter,
            IHubContext<ChatHub> hub)
        {
            _processor = processor;
            _formatter = formatter;
            _hub = hub;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(string message, string connectionId)
        {
            if (string.IsNullOrWhiteSpace(message))
                return BadRequest("Сообщение не может быть пустым.");

            // 1) Получаем структуру ApiResponse
            try { var apiResp = await _processor.ProcessMessageAsync(connectionId, message);
                var html = _formatter.ToHtml(apiResp);
                // 3) Отправляем клиенту через SignalR
                await _hub.Clients.Client(connectionId)
                    .SendAsync("ReceiveMessage", html);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            // 2) Форматируем в HTML
           

            return Ok();
        }

    }
}