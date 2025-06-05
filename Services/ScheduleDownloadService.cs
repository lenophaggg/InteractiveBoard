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
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await RunSchedulesDownload(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                DateTime nextRunTime = now.Date.AddDays(1).AddHours(2);
                TimeSpan delay = nextRunTime - now;

                _logger.LogInformation("Следующий запуск парсинга расписания запланирован на {NextRunTime}, через {Seconds} секунд", nextRunTime, delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break; // Остановка по требованию
                }

                await RunSchedulesDownload(cancellationToken);
            }
        }

        private async Task RunSchedulesDownload(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1) Очистка старых расписаний и связанных данных
                try
                {
                    await context.ClearOldSchedulesFacultiesClassroomsGroupsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при очистке старых данных расписания.");
                }

                // 2) Загрузка и сохранение факультетов и групп
                try
                {
                    await ManageAndSaveFacultiesAndGroups("https://www.smtu.ru/ru/listschedule/", context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при загрузке и сохранении факультетов и групп.");
                }

                // 3) Загрузка и сохранение расписаний для групп
                try
                {
                    await ParseAndSaveGroupSchedule("https://www.smtu.ru/ru/viewschedule/", context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при загрузке и сохранении расписаний для групп.");
                }

                // 4) Загрузка и сохранение расписаний для преподавателей
                try
                {
                    await ParseAndSavePersonSchedule(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при загрузке и сохранении расписаний для преподавателей.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в процессе обновления расписаний (корневой блок)");
            }
        }

        private async Task ManageAndSaveFacultiesAndGroups(string universityUrl, ApplicationDbContext context)
        {
            var web = new HtmlWeb();
            HtmlDocument doc;

            try
            {
                doc = await Task.Run(() => web.Load(universityUrl));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке страницы {Url}", universityUrl);
                return;
            }

            var nodes = doc.DocumentNode.SelectNodes("//h3[contains(@style, 'clear:both')]");
            if (nodes == null)
                return;

            foreach (var node in nodes)
            {
                var facultyName = node.InnerText.Trim();
                var existingFaculty = await context.Faculties.FirstOrDefaultAsync(f => f.Name == facultyName);

                if (existingFaculty == null)
                {
                    existingFaculty = new Faculties { Name = facultyName };
                    context.Faculties.Add(existingFaculty);
                }

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

            try
            {
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении факультетов и групп в БД.");
            }
        }

        private async Task ParseAndSaveGroupSchedule(string baseUrl, ApplicationDbContext context)
        {
            List<string> groupNumberList = null;
            try
            {
                groupNumberList = await context.Groups.Select(g => g.Number).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка групп из БД.");
                return;
            }

            if (groupNumberList == null || !groupNumberList.Any())
                return;

            foreach (var groupNumber in groupNumberList)
            {
                try
                {
                    await ProcessItemSchedule(baseUrl, groupNumber, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке расписания для группы {GroupNumber}", groupNumber);
                }
            }
        }

        private async Task ParseAndSavePersonSchedule(ApplicationDbContext context)
        {
            List<PersonContact> personList = null;
            try
            {
                personList = await context.PersonContacts.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка преподавателей.");
                return;
            }

            foreach (var person in personList)
            {
                if (string.IsNullOrEmpty(person.UniversityIdContact))
                    continue;

                var url = $"https://www.smtu.ru/ru/viewschedule/teacher/{person.UniversityIdContact}/";
                try
                {
                    await ProcessItemSchedule(url, person.UniversityIdContact, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке расписания для преподавателя {TeacherId}", person.UniversityIdContact);
                }
            }
        }

        private async Task ProcessItemSchedule(string baseUrl, string item, ApplicationDbContext context)
        {
            var url = baseUrl.EndsWith("/") ? (baseUrl + item + "/") : baseUrl;

            HtmlDocument doc = null;
            try
            {
                var web = new HtmlWeb();
                doc = await Task.Run(() => web.Load(url));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке {Url}, пропускаем", url);
                return;
            }

            var dayNodes = doc.DocumentNode.SelectNodes("//div[@class='card my-4']");
            if (dayNodes == null)
                return;

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

                    DateTime startDateTime, endDateTime;
                    try
                    {
                        startDateTime = DateTime.ParseExact(timeParts[0], "HH:mm", CultureInfo.InvariantCulture);
                        endDateTime = DateTime.ParseExact(timeParts[1], "HH:mm", CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        continue; // Невалидное время, пропустить
                    }
                    TimeSpan startTime = startDateTime.TimeOfDay;
                    TimeSpan endTime = endDateTime.TimeOfDay;

                    var weekTypeNode = timeNode.ParentNode.SelectSingleNode(
                        ".//td[contains(@class, 'text-warning')]/i " +
                        "| .//td[contains(@class, 'text-success')]/i " +
                        "| .//td[contains(@class, 'text-info')]/i");
                    var weekType = weekTypeNode?.Attributes["data-bs-title"]?.Value.Trim();

                    var classroomNumber = GetColumnValue(timeNode.ParentNode, 2);
                    var groupNumber = GetColumnValue(timeNode.ParentNode, 3);

                    var instructorNode = timeNode.ParentNode.SelectSingleNode(".//td[last()]");

                    string instructorName = string.Empty;
                    string extractedInstructorId = string.Empty;

                    if (instructorNode != null)
                    {
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

                    var subjectInfoNode = timeNode.ParentNode.SelectSingleNode(
                        ".//td//small[not(contains(@class, 'text-muted'))]");
                    var subjectInfo = subjectInfoNode?.InnerText.Trim();

                    var existingSubject = await context.Subjects.FirstOrDefaultAsync(s => s.SubjectName == subjectName);
                    if (existingSubject == null)
                    {
                        subjectInfo = subjectName ?? subjectInfo;
                        subjectName = null;
                    }

                    // Проверяем аудиторию
                    var existingClassroom = await context.Classrooms.FirstOrDefaultAsync(c => c.ClassroomNumber == classroomNumber);
                    if (existingClassroom == null && !string.IsNullOrEmpty(classroomNumber))
                    {
                        try
                        {
                            context.Classrooms.Add(new Classrooms { ClassroomNumber = classroomNumber });
                            await context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка при добавлении аудитории {Classroom}", classroomNumber);
                        }
                    }

                    // Проверяем, существует ли группа перед добавлением расписания
                    if (!string.IsNullOrWhiteSpace(groupNumber))
                    {
                        var groupExists = await context.Groups.AnyAsync(g => g.Number == groupNumber);
                        if (!groupExists)
                        {
                            _logger.LogError("Попытка добавить расписание для несуществующей группы: {Group}", groupNumber);
                            continue; // Пропустить этот слот расписания!
                        }
                    }

                    // Инструктор (опционально)
                    PersonContact existingInstructor = null;
                    if (!string.IsNullOrEmpty(extractedInstructorId))
                    {
                        existingInstructor = await context.PersonContacts.FirstOrDefaultAsync(p => p.UniversityIdContact == extractedInstructorId);
                    }
                    int? instructorDbId = existingInstructor?.IdContact;

                    try
                    {
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
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при добавлении расписания для группы {Group}", groupNumber);
                        continue;
                    }
                }
            }
        }

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
