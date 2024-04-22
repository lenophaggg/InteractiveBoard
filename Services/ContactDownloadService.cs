using HtmlAgilityPack;
using MyMvcApp.Models;
using Newtonsoft.Json;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MyMvcApp.Services
{
    public class ContactDownloadService : IHostedService
    {
        private static TimeSpan CheckInterval = TimeSpan.FromDays(1);
        private static readonly string personContactsPath = Path.Combine("wwwroot", "main_contact", "person_contacts.json");
        private CancellationTokenSource _cts;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Запуск цикла в отдельной задаче
            _ = DownloadContacts(_cts.Token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();

            return Task.CompletedTask;
        }

        private async Task DownloadContacts(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;

                // Проверяем, воскресенье ли сегодня и 12 часов ли сейчас
                if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 12 && now.Minute == 0)
                {
                    // Вызов метода для парсинга и сохранения контактов
                    ParseAndSavePersonContact("https://www.smtu.ru/ru/listdepartment/", personContactsPath);
                }

                // Вычисляем время до следующего запуска: следующее воскресенье в 12:00
                DateTime nextRunTime = now.Date.AddDays((7 - (int)now.DayOfWeek) % 7 + 1).AddHours(12);
                TimeSpan delay = nextRunTime - now;

                // Задержка до следующего запуска
                await Task.Delay(delay, cancellationToken);
            }

            //ParseAndSavePersonContact("https://www.smtu.ru/ru/listdepartment/", personContactsPath);
            //await Task.Delay(CheckInterval, cancellationToken);
        }

        private void ParseAndSavePersonContact(string universityUrl, string dataFolderPath)
        {
            var listdepartment = ParseDepartment(universityUrl);
            var personsContactList = ParseUnitEmployeesAndSaveEmailPhoneImg(listdepartment);
            UpdatePersonContacts(personsContactList);

            File.WriteAllText(personContactsPath, JsonConvert.SerializeObject(personsContactList, Formatting.Indented));

        }

        private List<string> ParseDepartment(string universityUrl)
        {
            var web = new HtmlWeb();
            var doc = web.Load(universityUrl);

            string xpath = "//h3[@class='h5 mt-4']/a[@href] | //li[@class='list-group-item']/h4/a[@href]";

            // Используем LINQ для извлечения ссылок и применения фильтрации
            var unitLinks = doc.DocumentNode.SelectNodes(xpath)
                .Select(linkNode => linkNode.GetAttributeValue("href", ""))
                .Where(link => !string.IsNullOrEmpty(link))
                .ToList();

            return unitLinks;
        }

        private List<PersonContact> ParseUnitEmployeesAndSaveEmailPhoneImg(List<string> unitLinks)
        {
            List<PersonContact> personContacts = new List<PersonContact>();
            HashSet<string> uniqueNames = new HashSet<string>(); // Используем HashSet для хранения уникальных имен
            HttpClient httpClient = new HttpClient(); // Этот клиент нужно будет создавать и использовать правильно для избежания утечек ресурсов

            foreach (var unit in unitLinks)
            {
                var web = new HtmlWeb();
                var doc = web.Load("https://www.smtu.ru" + unit); // Синхронная загрузка документа

                var cardDivs = doc.DocumentNode.SelectNodes("//div[contains(@class, 'card') and contains(@class, 'text-bg-light')]");

                if (cardDivs != null)
                {
                    foreach (var cardDiv in cardDivs)
                    {
                        // Поиск имени внутри тега <h2> с классом 'h5 text-info-dark'
                        var nameNode = cardDiv.SelectSingleNode(".//h2[@class='h5 text-info-dark']/a[@class='text-decoration-none']");
                        if (nameNode == null)
                        {
                            // Если тег <a> не найден, ищем имя напрямую в теге <h2>
                            nameNode = cardDiv.SelectSingleNode(".//h2[@class='h5 text-info-dark']");
                        }
                        if (nameNode == null) continue; // Если имя не найдено, пропускаем итерацию

                        var name = nameNode.InnerText.Trim();
                        if (!uniqueNames.Contains(name)) // Проверяем уникальность по имени
                        {
                            uniqueNames.Add(name); // Добавляем имя в набор уникальных имен

                            var person = new PersonContact
                            {
                                NameContact = name,
                                Email = cardDiv.SelectSingleNode(".//a[contains(@href, 'mailto:')]")?.InnerText.Trim(),
                                Telephone = cardDiv.SelectSingleNode(".//a[contains(@href, 'tel:')]")?.InnerText.Trim()
                            };

                            var img = cardDiv.SelectSingleNode(".//img");
                            if (img != null)
                            {
                                var src = img.GetAttributeValue("src", "");
                                src = src.Split('?')[0]; // Удаление параметров запроса из URL
                                  var pattern = @"https://isu\.smtu\.ru/images/isu_person/small/p(\d+)\.jpg";
                                var idMatch = Regex.Match(src, pattern);
                                if (idMatch.Success)
                                {
                                    var id = idMatch.Groups[1].Value;
                                    person.IdContact = id; // Сохранение ID как IdContact

                                    var bigImageUrl = src.Replace("small", "big");

                                    // Синхронная проверка существования изображения большого размера
                                    HttpResponseMessage response = httpClient.GetAsync(bigImageUrl).Result; // Используем .Result для синхронного ожидания
                                    if (response.IsSuccessStatusCode)
                                    {
                                        person.ImgPath = bigImageUrl; // Сохранение ссылки на изображение большого размера
                                    }
                                }
                            }

                            // Добавление персоны в список, даже если нет изображения
                            personContacts.Add(person);
                        }
                    }
                }
            }

            return personContacts;
        }

        private void UpdatePersonContacts(List<Models.PersonContact> personContacts)
        {
            var web = new HtmlWeb();

            foreach (var personContact in personContacts)
            {
                var htmlDoc = web.Load($"https://isu.smtu.ru/view_user_page/{personContact.IdContact}/");

                // Основной блок, который содержит нужную информацию
                var mainNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='warper container-fluid']/div[@class='panel panel-default']");
                if (mainNode == null) continue;

                // Обновление имени
                var nameNode = mainNode.SelectSingleNode(".//h3[@itemprop='fio']");
                personContact.NameContact = nameNode?.InnerText;

                // Обновление должностей
                var positionNodes = mainNode.SelectNodes(".//div[@itemprop='post']/li");
                personContact.Position = positionNodes?.Select(n => n.InnerText.Trim()).ToList();

                // Обновление преподаваемых предметов
                var taughtSubjectsNodes = mainNode.SelectNodes(".//div[@itemprop='teachingDiscipline']/li");
                personContact.TaughtSubjects = taughtSubjectsNodes?.Select(n => n.InnerText.Trim()).ToList();

                // Обновление ученой степени
                var academicDegreeNode = mainNode.SelectSingleNode(".//div[@itemprop='degree']/li");
                personContact.AcademicDegree = academicDegreeNode?.InnerText;

                // Обновление опыта преподавания
                var teachingExperienceNode = mainNode.SelectSingleNode(".//div[@itemprop='specExperience']/li");
                personContact.TeachingExperience = teachingExperienceNode?.InnerText;
            }
        }






    }
}