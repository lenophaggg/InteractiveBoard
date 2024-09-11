using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Globalization;
using System.Data.SqlClient;

using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace MyMvcApp.Services
{
    public class ScheduleDownloadService : IHostedService
    {
        private static TimeSpan CheckInterval = TimeSpan.FromDays(1); // Интервал проверки 1 день
        private CancellationTokenSource _cts;
        private readonly IServiceProvider _serviceProvider;

        public ScheduleDownloadService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = DownloadSchedules(_cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            return Task.CompletedTask;
        }

        private async Task DownloadSchedules(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;

                // Проверяем, 02:00 ли сейчас
                if (now.Hour == 2 && now.Minute == 0)
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // Очистка старых расписаний
                        await context.ClearOldSchedulesFacultiesClassroomsGroupsAsync();

                        // Загрузка и сохранение факультетов и групп
                        await ManageAndSaveFacultiesAndGroups("https://www.smtu.ru/ru/listschedule/", context);

                        // Загрузка и сохранение расписаний для групп
                        await ParseAndSaveGroupSchedule("https://www.smtu.ru/ru/viewschedule/", context);

                        // Загрузка и сохранение расписаний для преподавателей
                        await ParseAndSavePersonSchedule(context);
                    }
                }

                // Вычисляем время до следующего запуска: следующий день в 02:00
                DateTime nextRunTime = now.Date.AddDays(1).AddHours(2);
                TimeSpan delay = nextRunTime - now;

                // Задержка до следующего запуска
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Task was canceled, exit the loop
                    break;
                }
            }
        }

        private async Task ManageAndSaveFacultiesAndGroups(string universityUrl, ApplicationDbContext context)
        {
            var web = new HtmlWeb();
            var doc = await Task.Run(() => web.Load(universityUrl));

            var nodes = doc.DocumentNode.SelectNodes("//h3[contains(@style, 'clear:both')]");
            foreach (var node in nodes)
            {
                var facultyName = node.InnerText.Trim();
                var existingFaculties = await context.Faculties.FirstOrDefaultAsync(f => f.Name == facultyName);

                if (existingFaculties == null)
                {
                    existingFaculties = new Faculties { Name = facultyName };
                    context.Faculties.Add(existingFaculties);
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
                            var existingGroup = await context.Groups.FirstOrDefaultAsync(g => g.Number == groupNumber && g.FacultyName == facultyName);

                            if (existingGroup == null)
                            {
                                var group = new Groups { Number = groupNumber, FacultyName = facultyName };
                                context.Groups.Add(group);
                            }
                        }
                    }
                    next = next.NextSibling;
                }
            }
            await context.SaveChangesAsync(); // Сохранение изменений в базе данных
        }

        private async Task ParseAndSaveGroupSchedule(string url, ApplicationDbContext context)
        {
            var groupNumberList = await context.Groups.Select(g => g.Number).ToListAsync();

            if (groupNumberList != null && groupNumberList.Any())
            {
                foreach (var groupNumber in groupNumberList)
                {
                    await ProcessItemSchedule(url, groupNumber, context);
                }
            }
        }
              

        private async Task ProcessItemSchedule(string url, string item, ApplicationDbContext context)
        {
            var web = new HtmlWeb();
            HtmlDocument doc;

            try
            {
                doc = await Task.Run(() => web.Load(url + item + "/"));
            }
            catch
            {
                // Ожидание перед повторной загрузкой
                await Task.Delay(10000);

                // Попытка повторной загрузки
                try
                {
                    doc = await Task.Run(() => web.Load(url + item + "/"));
                }
                catch
                {
                    return;
                }
            }

            var dayNodes = doc.DocumentNode.SelectNodes("//div[@class='card my-4']");

            if (dayNodes != null)
            {
                foreach (var dayNode in dayNodes)
                {
                    var dayOfWeek = dayNode.SelectSingleNode(".//div[@class='card-header']/h3")?.InnerText.Trim();
                    var timeNodes = dayNode.SelectNodes(".//tr/th[@scope='row']");

                    if (timeNodes != null)
                    {
                        foreach (var timeNode in timeNodes)
                        {
                            var timeRange = timeNode.InnerText.Trim();
                            var timeParts = timeRange?.Split('-');

                            DateTime startDateTime = DateTime.ParseExact(timeParts[0], "HH:mm", CultureInfo.InvariantCulture);
                            TimeSpan startTime = startDateTime.TimeOfDay;

                            DateTime endDateTime = DateTime.ParseExact(timeParts[1], "HH:mm", CultureInfo.InvariantCulture);
                            TimeSpan endTime = endDateTime.TimeOfDay;

                            var weekTypeNode = timeNode.ParentNode.SelectSingleNode(".//td[contains(@class, 'text-warning')]/i " +
                                "| .//td[contains(@class, 'text-success')]/i | .//td[contains(@class, 'text-info')]/i");
                            var weekType = weekTypeNode?.Attributes["data-bs-title"]?.Value.Trim();

                            var classroomNumber = GetColumnValue(timeNode.ParentNode, 2);
                            var groupNumber = GetColumnValue(timeNode.ParentNode, 3);

                            var instructorNode = timeNode.ParentNode.SelectSingleNode(".//td[last()]");

                            string instructorName = string.Empty;
                            string instructorId = string.Empty;

                            if (instructorNode != null)
                            {
                                var imgNode = instructorNode.SelectSingleNode(".//img");
                                if (imgNode != null)
                                {
                                    var instructorImageUrl = imgNode.GetAttributeValue("src", "");
                                    instructorImageUrl = instructorImageUrl.Split('?')[0]; // Удаление параметров запроса из URL

                                    var pattern = @"https://isu\.smtu\.ru/images/isu_person/small/p(\d+)\.jpg";
                                    var idMatch = Regex.Match(instructorImageUrl, pattern);
                                    if (idMatch.Success)
                                    {
                                        instructorId = idMatch.Groups[1].Value;
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
                            var subjectName = subjectNode?.InnerText.Trim();

                            var subjectInfoNode = timeNode.ParentNode.SelectSingleNode(".//td//small[not(contains(@class, 'text-muted'))]");
                            var subjectInfo = subjectInfoNode?.InnerText.Trim();

                            var existingSubject = await context.Subjects.FirstOrDefaultAsync(s => s.SubjectName == subjectName);
                            if (existingSubject == null)
                            {
                                subjectInfo = subjectName;
                                subjectName = null;
                            }

                            var existingClassroom = await context.Classrooms.FirstOrDefaultAsync(c => c.ClassroomNumber == classroomNumber);
                            if (existingClassroom == null)
                            {
                                var classroom = new Classrooms { ClassroomNumber = classroomNumber };
                                context.Classrooms.Add(classroom);
                                await context.SaveChangesAsync();
                            }

                            var existingGroup = await context.Groups.FirstOrDefaultAsync(g => g.Number == groupNumber);
                            if (existingGroup == null)
                            {
                                var group = new Groups { Number = groupNumber };
                                context.Groups.Add(group);
                                await context.SaveChangesAsync();
                            }

                            PersonContact? existingInstructor = null;

                            if (!string.IsNullOrEmpty(instructorId))
                            {
                                existingInstructor = await context.PersonContacts
                                    .FirstOrDefaultAsync(p => p.UniversityIdContact == instructorId);
                            }

                            // Проверка на наличие существующей записи
                            bool scheduleExists = await context.ScheduleData.AnyAsync(sd =>
                                sd.DayOfWeek == dayOfWeek &&
                                sd.StartTime == startTime &&
                                sd.EndTime == endTime &&
                                sd.WeekType == weekType &&
                                sd.Classroom == classroomNumber &&
                                sd.Group == groupNumber &&
                                sd.Subject == subjectName &&
                                sd.InstructorId == (existingInstructor != null ? existingInstructor.IdContact : (int?)null) &&
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
                                    InstructorId = existingInstructor?.IdContact,
                                    ScheduleInfo = subjectInfo
                                };

                                context.ScheduleData.Add(newScheduleData);
                                await context.SaveChangesAsync();
                            }
                        }
                    }
                }
            }
        }

        private async Task ParseAndSavePersonSchedule(ApplicationDbContext context)
        {
            var personList = await context.PersonContacts.ToListAsync();

            foreach (var person in personList)
            {
                // Формируем полный URL для каждого преподавателя по UniversityIdContact
                var fullPersonUrl = new Uri($"https://www.smtu.ru/ru/viewschedule/teacher/{person.UniversityIdContact}/").AbsoluteUri;

                // Реализуем логику парсинга и сохранения расписания для каждого преподавателя
                await ProcessItemSchedule(fullPersonUrl, person.UniversityIdContact, context);
            }
        }

        private string GetColumnValue(HtmlNode dayNode, int columnNumber)
        {
            var columnNode = dayNode.SelectSingleNode($".//td[{columnNumber}]");
            return columnNode?.InnerText.Trim() ?? "";
        }
    }
}
