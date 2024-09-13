using Microsoft.AspNetCore.Mvc;

namespace MyMvcApp.Controllers
{
    public class MapController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
