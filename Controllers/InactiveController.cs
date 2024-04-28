using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using Newtonsoft.Json;


namespace MyMvcApp.Controllers
{
    public class InactiveController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IConfiguration _configuration;

        public InactiveController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment, IConfiguration configuration)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _configuration = configuration;
        }

        public IActionResult Index()
        {            
            return View();
        }

        [HttpGet]
        public IActionResult GetCurrentSchedule()
        {
            List<MyMvcApp.Models.CurrentSchedule> scheduleItems = new List<MyMvcApp.Models.CurrentSchedule>();

            var dataParser = new DataParserModel();
            string facultyCode = _configuration["ScheduleOptions:Faculty"];

            var nameFolder = dataParser.GetFacultyName(facultyCode);

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

            string[] jsonFiles = Directory.GetFiles(Path.Combine(_hostingEnvironment.WebRootPath, "schedules", "faculties_schedules", nameFolder), "*.json");

            for (int i = 0; i < startTimes.Length; i++)
            {
                if (currentTime.TimeOfDay >= startTimes[i] && currentTime.TimeOfDay <= endTimes[i])
                {
                    status = "Ожидается";

                    currentTime = currentTime.AddMinutes(90);

                    break; // Прекращаем итерацию, как только найден попадающий промежуток
                }
            }

            foreach (string jsonFile in jsonFiles)
            {
                // Читаем содержимое JSON-файла
                string jsonContent = System.IO.File.ReadAllText(jsonFile);

                // Десериализуем JSON-строку в список объектов ScheduleItem
                List<MyMvcApp.Models.ScheduleData> items = JsonConvert.DeserializeObject<List<MyMvcApp.Models.ScheduleData>>(jsonContent);

                // Фильтруем объекты по заданным критериям
                foreach (MyMvcApp.Models.ScheduleData item in items)
                {
                    if ((item.WeekType == typeWeek &&
                        item.DayOfWeek == currentDayOfWeek &&
                        item.StartTime <= currentTime.TimeOfDay &&
                        item.EndTime >= currentTime.TimeOfDay)
                        || (
                        item.WeekType == "Обе недели" &&
                        item.DayOfWeek == currentDayOfWeek &&
                        item.StartTime <= currentTime.TimeOfDay &&
                        item.EndTime >= currentTime.TimeOfDay))
                    {
                        scheduleItems.Add(new MyMvcApp.Models.CurrentSchedule
                        {
                            Group = Path.GetFileNameWithoutExtension(jsonFile),
                            Subject = item.Subject,
                            Classroom = item.Classroom,
                            InstructorName = item.InstructorName,
                            Status = status
                        });
                    }
                }
            }

            return PartialView("_DIT", scheduleItems);

        }
    }
}
