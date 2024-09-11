using Microsoft.AspNetCore.Mvc;

namespace MyMvcApp.Controllers
{
    public class ScienceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
