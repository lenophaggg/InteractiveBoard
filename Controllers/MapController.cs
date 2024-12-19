using Microsoft.AspNetCore.Mvc;

namespace MyMvcApp.Controllers
{
    public class MapController : Controller
    {

        // Экшен Index принимает необязательный параметр roomId
        [HttpGet]
        public IActionResult Index(string roomId)
        {
            // Передаём roomId во View через ViewBag (или ViewData/модель — на вкус)
            ViewBag.PreselectedRoom = roomId;
            return View();
        }

    }
}
