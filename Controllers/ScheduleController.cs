using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;

namespace MyMvcApp.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public ScheduleController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
        }

        public IActionResult Index()
        {
            string facultiesSchedulePath = Path.Combine(_hostingEnvironment.WebRootPath, "schedules", "faculties_schedules");
            Dictionary<string, string[]> facultyGroups = new Dictionary<string, string[]>();

            string[] subDirectories = Directory.GetDirectories(facultiesSchedulePath);

            foreach (string subDirectory in subDirectories)
            {
                string facultyName = Path.GetFileName(subDirectory);
                var files = Directory.GetFiles(subDirectory).Select(fullPath => Path.GetFileNameWithoutExtension(fullPath)).ToArray();
                facultyGroups.Add(facultyName, files);
            }

            return View(facultyGroups);
        }

        [HttpGet]
        public IActionResult ClarifyPerson(string personTerm)
        {
            string directoryPath = Path.Combine(_hostingEnvironment.WebRootPath, $"schedules/person_schedules");
            string[] files = Directory.GetFiles(directoryPath, "*" + personTerm + "*");

            List<string> fileNames = new List<string>();
            foreach (var file in files)
            {
                fileNames.Add(Path.GetFileNameWithoutExtension(file.Replace("_", " ")));
            }

            ViewData["Type"] = "Schedule";

            return PartialView("~/Views/Shared/_PotentialPersonList.cshtml", fileNames);

        }


        [HttpGet]
        public IActionResult GetScheduleForSearchTerm(string searchTerm, string facultyName = "")
        {
            // Проверяем, является ли поисковый запрос числом
            if (int.TryParse(searchTerm, out _))
            {
                return GetScheduleByGroup(searchTerm, facultyName);
            }
            else
            {
                return GetScheduleByPerson(searchTerm);
            }
        }

        // Метод GetScheduleByGroup
        [HttpGet]
        public IActionResult GetScheduleByGroup(string groupNumber, string facultyName = "")
        {
            var dataParser = new DataParserModel();
            string errorMessage;

            if (!IsValidGroupNumber(groupNumber, out errorMessage))
            {
                // Если номер группы некорректный, добавляем сообщение об ошибке в ViewBag
                ViewBag.ErrorMessage = errorMessage;

                // Возвращаем частичное представление с сообщением об ошибке
                return PartialView("_SсheduleGroup", new List<Models.ScheduleData>());
            }

            try
            {
                List<Models.ScheduleData> scheduleData = dataParser.LoadDataFromJson<Models.ScheduleData>(Path.Combine(_hostingEnvironment.WebRootPath, $"schedules/faculties_schedules/{facultyName}/{groupNumber}.json"));

                // Возвращаем частичное представление с расписанием
                return PartialView("_SсheduleGroup", scheduleData);
            }
            catch (Exception)
            {
                List<Models.ScheduleData> scheduleData = new List<Models.ScheduleData>();
                ViewBag.ErrorMessage = "Проверьте правильность номера группы";

                // Возвращаем частичное представление с расписанием
                return PartialView("_SсheduleGroup", scheduleData);
            }

        }

        [HttpGet]
        public PartialViewResult GetScheduleByPerson(string personName)
        {
            personName = personName.Replace(" ", "_");

            var dataParser = new DataParserModel();
            string filePath = Path.Combine(_hostingEnvironment.WebRootPath, "schedules", "person_schedules", $"{personName}.json");

            // Проверяем, существует ли файл
            if (!System.IO.File.Exists(filePath))
            {
                // Файл не найден, добавляем сообщение об ошибке в ModelState
                ModelState.AddModelError(string.Empty, $"Расписание для преподавателя {personName} не найдено.");
                // Возвращаем частичное представление с сообщением об ошибке
                return PartialView("_SchedulePerson", new List<Models.ScheduleData>());
            }

            // Загрузите расписание для указанного пользователя
            List<Models.ScheduleData> scheduleData = dataParser.LoadDataFromJson<Models.ScheduleData>(filePath);

            // Очистите ModelState от предыдущих ошибок
            ModelState.Clear();

            // Возвращаем частичное представление с расписанием
            return PartialView("_SchedulePerson", scheduleData);
        }

        private bool IsValidGroupNumber(string groupNumber, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(groupNumber))
            {
                errorMessage = "Поле ввода не должно быть пустым";
                return false;
            }
            // Проверка на длину числа (4-5 символов)
            if (!(groupNumber.Length == 4 || groupNumber.Length == 5))
            {
                errorMessage = "Проверьте правильность номера группы";
                return false;
            }

            return true;
        }
    }
}
