using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using Newtonsoft.Json;


namespace MyMvcApp.Controllers
{
    public class InactiveController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public InactiveController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment, IConfiguration configuration, ApplicationDbContext context)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _configuration = configuration;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GetCurrentSchedule()
        {
            //var allSchedule = _context.ScheduleData.ToList();

            List<CurrentSchedule> scheduleItems = new List<CurrentSchedule>();

            var dataParser = new DataParserModel();
            string nameFaculty = _configuration["ScheduleOptions:Faculty"];
            

            var allScheduleForFaculty = _context.ScheduleData
                .Where(s => _context.Groups
                                .Where(g => g.FacultyName == nameFaculty)
                                .Select(g => g.Number)
                    .Contains(s.Group))
                    .Include(s => s.Instructor)
                .ToList();

            // Получение текущего дня недели
            string currentDayOfWeek = DateTime.Today.ToString("dddd", new System.Globalization.CultureInfo("ru-RU"));
            currentDayOfWeek = char.ToUpper(currentDayOfWeek[0]) + currentDayOfWeek.Substring(1);

            var typeWeek = _configuration["ScheduleOptions:TypeWeek"];

            // Получение текущего времени
            DateTime currentTime = DateTime.Now;

            //DateTime currentTime = DateTime.Today.AddHours(11).AddMinutes(20);

            string status = "Идет";

            // Определение промежутков времени для каждой пары
            TimeSpan[] startTimes = new TimeSpan[]
            {
                new TimeSpan(7, 0, 0),
                new TimeSpan(10, 0, 0),
                new TimeSpan(11, 40, 0),
                new TimeSpan(13, 20, 0),
                new TimeSpan(15, 30, 0),
                new TimeSpan(17, 10, 0),
                new TimeSpan(18, 50, 0),
                new TimeSpan(20, 30, 0)
            };

            TimeSpan[] endTimes = new TimeSpan[]
            {
                new TimeSpan(8, 30, 0),
                new TimeSpan(10, 10, 0),
                new TimeSpan(11, 50, 0),
                new TimeSpan(14, 0, 0),
                new TimeSpan(15, 40, 0),
                new TimeSpan(17, 20, 0),
                new TimeSpan(19, 0, 0),
                new TimeSpan(20, 40, 0)
            };
                        
            for (int i = 0; i < startTimes.Length; i++)
            {
                if (currentTime.TimeOfDay >= startTimes[i] && currentTime.TimeOfDay <= endTimes[i])
                {
                    status = "Ожидается";

                    currentTime = currentTime.AddMinutes(90);

                    break; // Прекращаем итерацию, как только найден попадающий промежуток
                }
            }

            foreach (var scheduleGroup in allScheduleForFaculty)
            {
                if ((scheduleGroup.WeekType == typeWeek || scheduleGroup.WeekType == "Обе недели") &&
                    scheduleGroup.DayOfWeek == currentDayOfWeek &&
                    scheduleGroup.StartTime <= currentTime.TimeOfDay &&
                    scheduleGroup.EndTime >= currentTime.TimeOfDay)                    
                {
                    scheduleItems.Add(new CurrentSchedule
                    {
                        Group = scheduleGroup.Group,
                        Subject = scheduleGroup.Subject,
                        Classroom = scheduleGroup.Classroom,
                        InstructorName = scheduleGroup.Instructor?.NameContact ?? "", // Обрабатываем null
                        Status = status, 
                        ScheduleInfo = scheduleGroup.ScheduleInfo
                    });
                }                
            }

            return PartialView("_DIT", scheduleItems);

        }
    }
}
