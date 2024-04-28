using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MyMvcApp.Services
{
    public class ScheduleDownloadService : IHostedService
    {
        private static TimeSpan CheckInterval = TimeSpan.FromDays(1);
        private static readonly string facultiesSchedulePath = Path.Combine("wwwroot", "schedules", "faculties_schedules");
        private static readonly string personSchedulePath = Path.Combine("wwwroot", "schedules", "person_schedules");
        private static readonly string personContactsPath = Path.Combine("wwwroot", "main_contact", "person_contacts.json");
        private CancellationTokenSource _cts;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Запуск цикла в отдельной задаче
            _ = DownloadSchedules(_cts.Token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();

            return Task.CompletedTask;
        }

        private async Task DownloadSchedules(CancellationToken cancellationToken)
        {
            //while (!cancellationToken.IsCancellationRequested)
            //{
            //    var now = DateTime.Now;

            //    if ((now.Month == 8 && now.Day >= 20) || (now.Month == 9 && now.Day <= 15) ||
            //        (now.Month == 1 && now.Day >= 20) || (now.Month == 2 && now.Day <= 15))
            //    {
            //        if (now.Hour == 1 && now.Minute == 0)
            //        {
            //            ParseAndSaveGroupSchedule("https://www.smtu.ru/ru/listschedule/", facultiesSchedulePath);
            //            ParseAndSavePersonSchedule(personSchedulePath);
            //        }

            //        DateTime nextTime;

            //        if (now.Hour >= 1)
            //        {
            //            nextTime = now.Date.AddDays(1).AddHours(1);
            //        }
            //        else
            //        {
            //            nextTime = now.Date.AddMinutes(60 - now.Minute);
            //        }

            //        CheckInterval = nextTime - now;
            //    }
            //    else
            //    {
            //        if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 23 && now.Minute == 00)
            //        {
            //            ParseAndSaveGroupSchedule("https://www.smtu.ru/ru/listschedule/", facultiesSchedulePath);
            //            ParseAndSavePersonSchedule(personSchedulePath);
            //        }

            //        var nextTime = now.Date.AddDays((7 - (int)now.DayOfWeek) % 7).AddHours(23);
            //        CheckInterval = nextTime - now;
            //    }

            //    await Task.Delay(CheckInterval, cancellationToken);
            //}
            ParseAndSaveGroupSchedule("https://www.smtu.ru/ru/listschedule/", facultiesSchedulePath);
            ParseAndSavePersonSchedule(personSchedulePath);
            await Task.Delay(CheckInterval, cancellationToken);
        }
        #region Group
        private void ParseAndSaveGroupSchedule(string universityUrl, string dataFolderPath)
        {
            var web = new HtmlWeb();
            var doc = web.Load(universityUrl);

            var groupLinks = doc.DocumentNode.SelectNodes("//div[@class='gr']/a[@href]")
                .Select(linkNode => Regex.Match(linkNode.GetAttributeValue("href", ""), @"\d+").Value)
                .Where(link => !string.IsNullOrEmpty(link))
                //.Where(linkNode => linkNode.Length == 5 && linkNode.StartsWith("20"))
                .ToList();

            foreach (var groupLink in groupLinks)
            {
                // Формируем полный URL для каждой группы
                var fullGroupUrl = new Uri($"https://www.smtu.ru/ru/viewschedule/{groupLink}/").AbsoluteUri;

                // Реализуем логику парсинга и сохранения расписания для каждой группы
                var scheduleData = ParseScheduleForItem(fullGroupUrl);

                // Получаем название папки для каждой группы на основе номера группы
                var facultyFolderName = GetFacultyFolderName(groupLink);

                // Создаем путь к папке для каждой группы
                var groupFolderPath = Path.Combine(dataFolderPath, facultyFolderName);

                // Создаем папку, если ее нет
                Directory.CreateDirectory(groupFolderPath);

                // Формируем путь к JSON-файлу для каждой группы
                var jsonFilePath = Path.Combine(groupFolderPath, $"{groupLink}.json");

                // Сохраняем данные в JSON-файл
                File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(scheduleData, Formatting.Indented));
            }
        }

        private string GetFacultyFolderName(string groupNumber)
        {
            if (groupNumber.Length == 4)
            {
                switch (groupNumber[0])
                {
                    case '1':
                        return "shipbuilding_and_ocean_engineering";
                    case '2':
                        return "ship_power_engineering_and_automation";
                    case '3':
                        return "marine_instrument_engineering";
                    case '4':
                        return "engineering_and_economics";
                    case '5':
                    case '6':
                    case '7':
                        return "natural_sciences_and_humanities";
                    case '8':
                        return "college_of_SMTU";
                }
            }
            else if (groupNumber.Length == 5 && groupNumber.StartsWith("20"))
            {
                return "digital_industrial_technologies";
            }

            return "unknown_faculty";
        }
        #endregion

        private List<Models.ScheduleData> ParseScheduleForItem(string groupUrl)
        {
            var scheduleDataList = new List<Models.ScheduleData>();
            var web = new HtmlWeb();
            var doc = web.Load(groupUrl);

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

                            var classroom = GetColumnValue(timeNode.ParentNode, 2);
                            var group = GetColumnValue(timeNode.ParentNode, 3);

                            var instructorNode = timeNode.ParentNode.SelectSingleNode(".//td[last()]");

                            string instructorName = string.Empty;
                            string instructorLink = string.Empty;

                            if (instructorNode != null)
                            {
                                var anchorNode = instructorNode.SelectSingleNode(".//a");
                                if (anchorNode != null)
                                {
                                    instructorName = anchorNode.InnerText.Trim();
                                    instructorLink = "https://www.smtu.ru" + anchorNode.GetAttributeValue("href", "").Trim();
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
                            var subject = subjectNode?.InnerText.Trim();


                            scheduleDataList.Add(new Models.ScheduleData
                            {
                                DayOfWeek = dayOfWeek,
                                StartTime = startTime,
                                EndTime = endTime,
                                WeekType = weekType,
                                Classroom = classroom,
                                Group = group,
                                Subject = subject,
                                InstructorName = instructorName,
                                InstructorLink = instructorLink
                            });
                        }
                    }
                }
            }
            return scheduleDataList;
        }
        private string GetColumnValue(HtmlNode dayNode, int columnNumber)
        {
            var columnNode = dayNode.SelectSingleNode($".//td[{columnNumber}]");
            return columnNode?.InnerText.Trim() ?? "";
        }

        #region Person
        private void ParseAndSavePersonSchedule(string dataFolderPath)
        {
            // Читаем все данные из файла
            string jsonData = File.ReadAllText(personContactsPath);
            // Десериализуем JSON в динамический объект
            JArray jsonArray = JArray.Parse(jsonData);

            // Создаем список для хранения ID контактов и имен
            var personDetails = new List<(string Id, string Name)>();

            // Извлекаем IdContact и NameContact каждого объекта в массиве
            foreach (JObject item in jsonArray)
            {
                if ((item["IdContact"] != null && item["IdContact"].ToString() != "")||(item["IdContact"].ToString() != "")|| (item["NameContact"].ToString() != ""))
                {
                    var id = item["IdContact"].ToString();
                    var name = item["NameContact"]?.ToString().Replace(" ", "_");
                    personDetails.Add((Id: id, Name: name));
                }
            }

            foreach (var (Id, Name) in personDetails)
            {
                // Формируем полный URL для каждого преподавателя по ID
                var fullPersonUrl = new Uri($"https://www.smtu.ru/ru/viewschedule/teacher/{Id}/").AbsoluteUri;

                // Реализуем логику парсинга и сохранения расписания для каждого преподавателя
                var scheduleData = ParseScheduleForItem(fullPersonUrl);

                // Формируем путь к JSON-файлу для каждого преподавателя, используя его имя
                var jsonFilePath = Path.Combine(dataFolderPath, $"{Name}.json");

                // Сохраняем данные в JSON-файл
                File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(scheduleData, Formatting.Indented));
            }
        }
        #endregion

    }
}