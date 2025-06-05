using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;    // ← подключили вашу модель
using System.Text;        // для StringBuilder
using System.Threading.Tasks;

namespace MyMvcApp.Controllers
{
    public class ChatController : Controller
    {
        // GET: /Chat/
        public IActionResult Index()
        {
            // Вы можете передать сюда какие-то ViewData или ViewModel,
            // но для простоты достаточно чистого Index.cshtml.
            return View();
        }
    }
}
