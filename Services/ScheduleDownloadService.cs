using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;  // Убедитесь, что пакет установлен
using System.IO;

namespace MyMvcApp.Services
{
    public class ScheduleDownloadService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduleDownloadService> _logger;
        private CancellationTokenSource _cts;
        private Task _executingTask;

        public ScheduleDownloadService(IServiceProvider serviceProvider, ILogger<ScheduleDownloadService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ScheduleDownloadService запускается...");
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Запускаем задачу в фоне
            _executingTask = ExecuteAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ScheduleDownloadService останавливается...");
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Основной метод фоновой задачи:
        /// 1) Немедленно вызывает парсинг расписания.
        /// 2) Ждёт до 2:00 следующего дня и повторяет парсинг.
        /// </summary>
        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Первый запуск сразу
            await RunSchedulesDownload(cancellationToken);

            // Затем каждый день в 2:00
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                // Следующий запуск — завтра в 2:00
                DateTime nextRunTime = now.Date.AddDays(1).AddHours(2);
                TimeSpan delay = nextRunTime - now;

                _logger.LogInformation("Следующий запуск парсинга расписания запланирован на {NextRunTime}, через {Seconds} секунд",
                    nextRunTime, delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break; // Остановка
                }

                await RunSchedulesDownload(cancellationToken);
            }
        }

        /// <summary>
        /// Выполняет логику:
        /// 1) Очистка старых данных.
        /// 2) Парсинг факультетов/групп.
        /// 3) Парсинг расписаний групп.
        /// 4) Парсинг расписаний преподавателей.
        /// </summary>
        private async Task RunSchedulesDownload(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Начинаем обновление расписаний в {Time}", DateTime.Now);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1) Очистка старых расписаний и связанных данных
                await context.ClearOldSchedulesFacultiesClassroomsGroupsAsync();
                _logger.LogInformation("Старые расписания, факультеты, аудитории, группы - очищены");

                // 2) Загрузка и сохранение факультетов и групп
                await ManageAndSaveFacultiesAndGroups("https://www.smtu.ru/ru/listschedule/", context);
                _logger.LogInformation("Завершена загрузка и сохранение факультетов и групп");

                // 3) Загрузка и сохранение расписаний для групп
                await ParseAndSaveGroupSchedule("https://www.smtu.ru/ru/viewschedule/", context);
                _logger.LogInformation("Завершена загрузка и сохранение расписаний для групп");

                // 4) Загрузка и сохранение расписаний для преподавателей
                await ParseAndSavePersonSchedule(context);
                _logger.LogInformation("Завершена загрузка и сохранение расписаний для преподавателей");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в процессе обновления расписаний");
            }
            finally
            {
                _logger.LogInformation("Обновление расписаний завершено в {Time}", DateTime.Now);
            }
        }

        /// <summary>
        /// Парсит страницу /ru/listschedule/, формирует список факультетов/групп и сохраняет в БД.
        /// </summary>
        private async Task ManageAndSaveFacultiesAndGroups(string universityUrl, ApplicationDbContext context)
        {
            _logger.LogInformation("Загрузка факультетов/групп с {Url}", universityUrl);
            var web = new HtmlWeb();
            HtmlDocument doc;

            try
            {
                doc = await Task.Run(() => web.Load(universityUrl));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при загрузке страницы {Url}", universityUrl);
                return;
            }

            // Ищем <h3 style="clear:both">..., как в вашем исходном примере
            var nodes = doc.DocumentNode.SelectNodes("//h3[contains(@style, 'clear:both')]");
            if (nodes == null)
            {
                _logger.LogInformation("Не найдено ни одного факультета (h3 style='clear:both') на {Url}", universityUrl);
                return;
            }

            foreach (var node in nodes)
            {
                var facultyName = node.InnerText.Trim();
                var existingFaculty = await context.Faculties.FirstOrDefaultAsync(f => f.Name == facultyName);

                if (existingFaculty == null)
                {
                    existingFaculty = new Faculties { Name = facultyName };
                    context.Faculties.Add(existingFaculty);
                }

                // Вёрстка: после <h3> идут <div class="gr"> с группами, пока не встретим следующий <h3>
                var next = node.NextSibling;
                while (next != null && next.Name != "h3" && !next.OuterHtml.Contains("<br><br><br><br><br>"))
                {
                    if (next.Name == "div" && next.GetAttributeValue("class", "") == "gr")
                    {
                        var groupNode = next.SelectSingleNode(".//a");
                        if (groupNode != null)
                        {
                            var groupNumber = groupNode.InnerText.Trim();
                            var existingGroup = await context.Groups
                                .FirstOrDefaultAsync(g => g.Number == groupNumber && g.FacultyName == facultyName);

                            if (existingGroup == null)
                            {
                                var group = new Groups { Number = groupNumber, FacultyName = facultyName };
                                context.Groups.Add(group);

                                var actualGroupExists = await context.ActualGroups.AnyAsync(g => g.GroupNumber == groupNumber);
                                if (!actualGroupExists)
                                {
                                    context.ActualGroups.Add(new ActualGroup { GroupNumber = groupNumber });
                                }
                            }
                        }
                    }
                    next = next.NextSibling;
                }
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Сохранены факультеты/группы, всего факультетов: {Count}", nodes.Count);
        }

        /// <summary>
        /// Получаем список групп из БД и парсим расписание для каждой.
        /// </summary>
        private async Task ParseAndSaveGroupSchedule(string baseUrl, ApplicationDbContext context)
        {
            _logger.LogInformation("Загрузка расписаний групп из {BaseUrl}", baseUrl);
            var groupNumberList = await context.Groups.Select(g => g.Number).ToListAsync();

            if (groupNumberList == null || !groupNumberList.Any())
            {
                _logger.LogInformation("Список групп пуст, пропускаем парсинг расписания групп");
                return;
            }

            foreach (var groupNumber in groupNumberList)
            {
                await ProcessItemSchedule(baseUrl, groupNumber, context);
            }
        }

        /// <summary>
        /// Для каждого преподавателя парсим расписание по ссылке /ru/viewschedule/teacher/{UniversityIdContact}/
        /// </summary>
        private async Task ParseAndSavePersonSchedule(ApplicationDbContext context)
        {
            var personList = await context.PersonContacts.ToListAsync();

            foreach (var person in personList)
            {
                if (string.IsNullOrEmpty(person.UniversityIdContact))
                    continue;

                var url = $"https://www.smtu.ru/ru/viewschedule/teacher/{person.UniversityIdContact}/";
                await ProcessItemSchedule(url, person.UniversityIdContact, context);
            }
        }

        /// <summary>
        /// Унифицированный метод парсинга расписания (для группы или преподавателя).
        /// </summary>
        private async Task ProcessItemSchedule(string baseUrl, string item, ApplicationDbContext context)
        {
            // Если baseUrl уже содержит "{teacher}/", то item — это ID преподавателя,
            // иначе составляем URL для расписания группы
            var url = baseUrl.EndsWith("/") ? (baseUrl + item + "/") : baseUrl;
            _logger.LogDebug("Парсим расписание: {Url}", url);

            var web = new HtmlWeb();
            HtmlDocument doc = null;

            try
            {
                doc = await Task.Run(() => web.Load(url));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при загрузке {Url}, вторая попытка через 10сек", url);
                await Task.Delay(10000);

                try
                {
                    doc = await Task.Run(() => web.Load(url));
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Вторая попытка загрузки {Url} также неудачна, пропускаем", url);
                    return;
                }
            }

            // Ищем блоки дня: <div class="card my-4">
            var dayNodes = doc.DocumentNode.SelectNodes("//div[@class='card my-4']");
            if (dayNodes == null)
            {
                _logger.LogInformation("На странице {Url} нет блоков расписания (card my-4)", url);
                return;
            }

            foreach (var dayNode in dayNodes)
            {
                var dayOfWeek = dayNode.SelectSingleNode(".//div[@class='card-header']/h3")?.InnerText?.Trim();
                var timeNodes = dayNode.SelectNodes(".//tr/th[@scope='row']");

                if (timeNodes == null)
                    continue;

                foreach (var timeNode in timeNodes)
                {
                    var timeRange = timeNode.InnerText.Trim();
                    var timeParts = timeRange?.Split('-');
                    if (timeParts == null || timeParts.Length != 2)
                        continue;

                    // Парсим время начала и конца
                    DateTime startDateTime = DateTime.ParseExact(timeParts[0], "HH:mm", CultureInfo.InvariantCulture);
                    TimeSpan startTime = startDateTime.TimeOfDay;

                    DateTime endDateTime = DateTime.ParseExact(timeParts[1], "HH:mm", CultureInfo.InvariantCulture);
                    TimeSpan endTime = endDateTime.TimeOfDay;

                    // Тип недели (подсветка: text-warning, text-success, text-info)
                    var weekTypeNode = timeNode.ParentNode.SelectSingleNode(
                        ".//td[contains(@class, 'text-warning')]/i " +
                        "| .//td[contains(@class, 'text-success')]/i " +
                        "| .//td[contains(@class, 'text-info')]/i");
                    var weekType = weekTypeNode?.Attributes["data-bs-title"]?.Value.Trim();

                    // Аудитория, группа, предмет
                    var classroomNumber = GetColumnValue(timeNode.ParentNode, 2);
                    var groupNumber = GetColumnValue(timeNode.ParentNode, 3);

                    var instructorNode = timeNode.ParentNode.SelectSingleNode(".//td[last()]");

                    string instructorName = string.Empty;
                    string extractedInstructorId = string.Empty; // ID, извлекаемый из изображения

                    if (instructorNode != null)
                    {
                        // Извлекаем ID
                        var imgNode = instructorNode.SelectSingleNode(".//img");
                        if (imgNode != null)
                        {
                            var instructorImageUrl = imgNode.GetAttributeValue("src", "");
                            instructorImageUrl = instructorImageUrl.Split('?')[0];

                            var pattern = @"https://isu\.smtu\.ru/images/isu_person/small/p(\d+)\.jpg";
                            var idMatch = Regex.Match(instructorImageUrl, pattern);
                            if (idMatch.Success)
                            {
                                extractedInstructorId = idMatch.Groups[1].Value;
                            }
                        }

                        // ФИО преподавателя
                        var anchorNode = instructorNode.SelectSingleNode(".//a");
                        if (anchorNode != null)
                        {
                            instructorName = anchorNode.InnerText.Trim();
                        }
                        else
                        {
                            var spanNode = instructorNode.SelectSingleNode(".//span");
                            if (spanNode != null)
                            {
                                instructorName = spanNode.InnerText.Trim();
                            }
                        }
                    }

                    var subjectNode = timeNode.ParentNode.SelectSingleNode(".//td/span");
                    var subjectName = subjectNode?.InnerText?.Trim();

                    // Доп. информация (например, тип пары)
                    var subjectInfoNode = timeNode.ParentNode.SelectSingleNode(
                        ".//td//small[not(contains(@class, 'text-muted'))]");
                    var subjectInfo = subjectInfoNode?.InnerText.Trim();

                    // Если не нашли такой предмет в справочнике, считаем всё subjectInfo
                    var existingSubject = await context.Subjects.FirstOrDefaultAsync(s => s.SubjectName == subjectName);
                    if (existingSubject == null)
                    {
                        // Допускаем, что subjectName может быть null, а всё хранится в subjectInfo
                        subjectInfo = subjectName ?? subjectInfo;
                        subjectName = null;
                    }

                    // Проверяем аудиторию в базе
                    var existingClassroom = await context.Classrooms.FirstOrDefaultAsync(c => c.ClassroomNumber == classroomNumber);
                    if (existingClassroom == null && !string.IsNullOrEmpty(classroomNumber))
                    {
                        context.Classrooms.Add(new Classrooms { ClassroomNumber = classroomNumber });
                        await context.SaveChangesAsync();
                    }

                    // Обновляем таблицы групп (Diary и ActualGroups)
                    if (!string.IsNullOrWhiteSpace(groupNumber))
                    {
                        // Проверка и добавление в таблицу всех групп (Diary)
                        var existingGroup = await context.Groups.FirstOrDefaultAsync(g => g.Number == groupNumber);
                        if (existingGroup == null)
                        {
                            context.Groups.Add(new Groups { Number = groupNumber });
                        }

                        // Проверка и добавление в таблицу актуальных групп (Interactive Board)
                        var actualGroupExists = await context.ActualGroups.AnyAsync(ag => ag.GroupNumber == groupNumber);
                        if (!actualGroupExists)
                        {
                            context.ActualGroups.Add(new ActualGroup { GroupNumber = groupNumber });
                        }

                        await context.SaveChangesAsync();
                    }

                    PersonContact existingInstructor = null;
                    if (!string.IsNullOrEmpty(extractedInstructorId))
                    {
                        existingInstructor = await context.PersonContacts.FirstOrDefaultAsync(p => p.UniversityIdContact == extractedInstructorId);
                    }

                    // Сохраняем IdContact в локальной переменной
                    int? instructorDbId = existingInstructor?.IdContact;

                    // Проверка: нет ли уже такой записи в расписании
                    bool scheduleExists = await context.ScheduleData.AnyAsync(sd =>
                        sd.DayOfWeek == dayOfWeek &&
                        sd.StartTime == startTime &&
                        sd.EndTime == endTime &&
                        sd.WeekType == weekType &&
                        sd.Classroom == classroomNumber &&
                        sd.Group == groupNumber &&
                        sd.Subject == subjectName &&
                        sd.InstructorId == instructorDbId &&
                        sd.ScheduleInfo == subjectInfo
                    );

                    // Если такой записи нет – добавляем её
                    if (!scheduleExists)
                    {
                        var newScheduleData = new ScheduleData
                        {
                            DayOfWeek = dayOfWeek,
                            StartTime = startTime,
                            EndTime = endTime,
                            WeekType = weekType,
                            Classroom = classroomNumber,
                            Group = groupNumber,
                            Subject = subjectName,
                            InstructorId = instructorDbId,
                            ScheduleInfo = subjectInfo
                        };

                        context.ScheduleData.Add(newScheduleData);
                        await context.SaveChangesAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Простая утилита получения текста колонки <td[columnNumber]>
        /// </summary>
        private string GetColumnValue(HtmlNode parentRow, int columnNumber)
        {
            var columnNode = parentRow.SelectSingleNode($".//td[{columnNumber}]");
            return columnNode?.InnerText.Trim() ?? "";
        }

        public void Dispose()
        {
            _cts?.Cancel();
        }
    }
}
