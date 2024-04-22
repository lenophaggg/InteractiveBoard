using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using System.Data.SqlTypes;
using VkNet.Model;

namespace MyMvcApp.Controllers
{
    public class ContactsController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;


        public ContactsController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
        }

        public IActionResult Index()
        {
            string filePath = Path.Combine(_hostingEnvironment.WebRootPath, "main_contact", "main_university_contacts.json");
            List<Models.MainUniversityContact> contactDataList = new List<Models.MainUniversityContact>();

            var dataParser = new DataParserModel();

            contactDataList = dataParser.LoadDataFromJson<Models.MainUniversityContact>(filePath);

            return View(contactDataList);
        }

        [HttpGet]
        public IActionResult ClarifyPerson(string personTerm)
        {
            string directoryPath = Path.Combine(_hostingEnvironment.WebRootPath, "main_contact", "person_contacts.json");
            var dataParser = new DataParserModel();
            List<Models.PersonContact> allPersonContactDataList = new List<Models.PersonContact>();
            List<string> similarContacts = new List<string>();

            if (personTerm.Length != 1)
            {
                allPersonContactDataList = dataParser.LoadDataFromJson<Models.PersonContact>(directoryPath);

                foreach (var contact in allPersonContactDataList)
                {
                    if (FindSubstring(contact.NameContact.ToLower(), personTerm.ToLower()))
                    {
                        similarContacts.Add(contact.NameContact);
                    }
                }
            }
            
            ViewData["Type"] = "Contacts";

            return PartialView("~/Views/Shared/_PotentialPersonList.cshtml", similarContacts);
        }

        public static bool FindSubstring(string mainString, string substring)
        {
            return mainString.IndexOf(substring) != -1;
        }

        [HttpGet]
        public IActionResult GetContactPerson(string personName)
        {
            DataParserModel dataParser = new DataParserModel();
            string jsonFilePath = Path.Combine(_hostingEnvironment.WebRootPath, $"main_contact/person_contacts.json");

            var personsContact = dataParser.LoadDataFromJson<MyMvcApp.Models.PersonContact>(jsonFilePath);

            var personContact = personsContact.Find(contact => contact.NameContact == personName);
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
