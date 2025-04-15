using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using System.Collections.Generic;
using System.Linq;
using VkNet.Model;

namespace MyMvcApp.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ApplicationDbContext _context;

        public ScheduleController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment, ApplicationDbContext context)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _context = context;
        }

        public IActionResult Index()
        {
            Dictionary<string, string[]> facultyGroups = new Dictionary<string, string[]>();

            // Получаем только те факультеты, у которых есть актуальные группы и FacultyName не null
            var facultiesWithActualGroups = _context.Groups
                .Where(g => g.FacultyName != null && // Добавляем проверку на null
                    _context.ActualGroups
                        .Select(ag => ag.GroupNumber)
                        .Contains(g.Number))
                .Select(g => g.FacultyName)
                .Distinct()
                .ToArray();

            foreach (var faculty in facultiesWithActualGroups.Reverse())
            {
                var groups = _context.ActualGroups
                    .Where(ag => _context.Groups
                        .Where(g => g.FacultyName == faculty)
                        .Select(g => g.Number)
                        .Contains(ag.GroupNumber))
                    .Select(ag => ag.GroupNumber)
                    .ToArray();

                if (groups.Any()) // Добавляем только если есть актуальные группы
                {
                    facultyGroups.Add(faculty, groups);
                }
            }

            return View(facultyGroups);
        }

        [HttpGet]
        public IActionResult ClarifyPerson(string personTerm)
        {
            if (personTerm.Length < 3)
            {
                ViewBag.ErrorMessage = $"Никого не найдено. Попробуйте изменить запрос.";
                return PartialView("_SchedulePerson", new List<ScheduleData>());
            }

            if (personTerm == null)
            {
                ViewData["ErrorMessage"] = "Введите запрос для поиска.";
                return PartialView("~/Views/Shared/_PotentialPersonList.cshtml", new List<ScheduleData>());
            }

            string searchPattern = personTerm.ToLower();

            List<PersonContact> similarContacts = _context.PersonContacts
                 .Where(p => p.NameContact.ToLower().Contains(searchPattern))
                 .ToList();

            ViewData["Type"] = "Schedule";

            return PartialView("~/Views/Shared/_PotentialPersonList.cshtml", similarContacts);
        }

        [HttpGet]
        public IActionResult GetScheduleForSearchTerm(string searchTerm)
        {
            // Проверяем, является ли поисковый запрос числом
            if (int.TryParse(searchTerm, out _))
            {
                return GetScheduleByGroup(searchTerm);
            }
            else
            {
                return GetScheduleByPerson(searchTerm);
            }
        }

        // Метод GetScheduleByGroup
        [HttpGet]
        public IActionResult GetScheduleByGroup(string groupNumber)
        {
            string errorMessage;

            if (!IsValidGroupNumber(groupNumber, out errorMessage))
            {
                // Если номер группы некорректный, добавляем сообщение об ошибке в ViewBag
                ViewBag.ErrorMessage = errorMessage;

                // Возвращаем частичное представление с сообщением об ошибке
                return PartialView("_SсheduleGroup", new List<ScheduleData>());
            }

            List<ScheduleData> scheduleData = _context.ScheduleData
            .Where(s => s.Group == groupNumber)
                .Include(s => s.Instructor)
                .Include(s => s.ClassroomNumber)
                .Include(s => s.GroupNumber)
                .Include(s => s.SubjectName)
            .AsEnumerable()  // Переключаемся на клиентскую обработку
            .OrderBy(s => s.DayOfWeek, new DayOfWeekComparer()) // Клиентская сортировка по дням недели
            .ThenBy(s => s.StartTime) // Затем сортировка по времени начала
            .ToList();

            // Возвращаем частичное представление с расписанием
            return PartialView("_SсheduleGroup", scheduleData);
        }

        public class DayOfWeekComparer : IComparer<string>
        {
            private static readonly string[] daysOfWeek =
            {
                "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье"
            };

            public int Compare(string x, string y)
            {
                int indexX = Array.IndexOf(daysOfWeek, x);
                int indexY = Array.IndexOf(daysOfWeek, y);
                return indexX.CompareTo(indexY);
            }
        }

        [HttpGet]
        public PartialViewResult GetScheduleByPerson(string personName, string universityIdContact = null)
        {
            var person = _context.PersonContacts.FirstOrDefault(p => p.NameContact == personName && p.UniversityIdContact == universityIdContact);

            // Проверяем, существует ли файл 
            if (person == null)
            {
                // Файл не найден, добавляем сообщение об ошибке в ModelState
                ViewBag.ErrorMessage = $"Преподаватель {personName} не найден";
                // Возвращаем частичное представление с сообщением об ошибке
                return PartialView("_SchedulePerson", new List<ScheduleData>());
            }

            // Загрузите расписание для указанного пользователя
            List<ScheduleData> scheduleData = _context.ScheduleData
            .Where(s => s.InstructorId == person.IdContact)
                .Include(s => s.Instructor)
                .Include(s => s.ClassroomNumber)
                .Include(s => s.GroupNumber)
                .Include(s => s.SubjectName)
            .AsEnumerable()  // Переключаемся на клиентскую обработку
            .OrderBy(s => s.DayOfWeek, new DayOfWeekComparer()) // Клиентская сортировка по дням недели
            .ThenBy(s => s.StartTime) // Затем сортировка по времени начала
            .ToList();

            if (!scheduleData.Any())
            {
                // Файл не найден, добавляем сообщение об ошибке в ModelState
                ViewBag.ErrorMessage = $"Расписание для преподавателя {personName} не найдено";
                // Возвращаем частичное представление с сообщением об ошибке
                return PartialView("_SchedulePerson", new List<ScheduleData>());
            }
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
