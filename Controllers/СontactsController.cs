using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using System.Data.SqlTypes;
using System.Linq;
using VkNet.Model;

namespace MyMvcApp.Controllers
{
    public class ContactsController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ApplicationDbContext _context;

        public ContactsController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment, ApplicationDbContext context)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _context = context;
        }

        public IActionResult Index()
        {
            var contactDataList = _context.MainUniversityContacts
              .OrderBy(c => c.NameContact)
              .ToList();

            return View(contactDataList);
        }

        [HttpGet]
        public IActionResult ClarifyPerson(string personTerm, string universityIdContact)
        {
            if (personTerm == null)
            {
                ViewData["ErrorMessage"] = "Введите термин для поиска.";
                return PartialView("~/Views/Shared/_PotentialPersonList.cshtml", new List<ScheduleData>());
            }

            string searchPattern = personTerm.ToLower();

            List<PersonContact> similarContacts = _context.PersonContacts
                 .Where(p => p.NameContact.ToLower().Contains(searchPattern))                 
                 .ToList();

            ViewData["Type"] = "Contacts";

            return PartialView("~/Views/Shared/_PotentialPersonList.cshtml", similarContacts);
        }

        [HttpGet]
        public IActionResult GetContactPerson(string personName, string universityIdContact)
        {
            var personContact = _context.PersonContacts
                .Where(p => p.NameContact == personName && p.UniversityIdContact == universityIdContact)
                    .Include(p => p.TaughtSubjects)  // Убедитесь, что у вас есть навигационное свойство TaughtSubjects в классе PersonContact
                .FirstOrDefault();

            if (personContact != null)
            {
                return PartialView("~/Views/Shared/_ContactPersonInfo.cshtml", personContact);
            }
            else
            {
                return null;
            }
        }
    }
}
