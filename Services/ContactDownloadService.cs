using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyMvcApp.Services
{
    public class ContactDownloadService : IHostedService
    {
        
        private CancellationTokenSource _cts;
        private readonly IServiceProvider _serviceProvider;

        public ContactDownloadService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = DownloadContacts(_cts.Token);
            return Task.CompletedTask;
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

                //// Проверяем, 22:00 ли сейчас
                if (now.Hour == 22 && now.Minute == 0)
                {                    
                   await ParseAndSavePersonContact();
                }

                // Вычисляем время до следующего запуска: следующий день в 22:00
                DateTime nextRunTime = now.Date.AddDays(1).AddHours(22);
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

        private async Task ParseAndSavePersonContact()
        {
            var listdepartment = await ParseDepartment("https://www.smtu.ru/ru/listdepartment/");

            try {
                foreach (var department in listdepartment)
                {
                    await ParseUnitEmployeesAndSaveNameUniversityIdEmailPhoneImg(department);
                }
            }
            catch(Exception ex) {
            
              
            }

           
            
            await UpdatePersonContacts();
        }

        private async Task<List<string>> ParseDepartment(string universityUrl)
        {
            var web = new HtmlWeb();
            var doc = await Task.Run(() => web.Load(universityUrl));

            // Находим секцию с нужным классом
            var sectionNode = doc.DocumentNode.SelectSingleNode("//section[contains(@class, 'bg-secondary') and contains(@class, 'pb-5')]");

            if (sectionNode == null)
            {
                return new List<string>();  // Если секция не найдена, возвращаем пустой список
            }

            // XPath для всех ссылок на подразделения, кафедры и базовые кафедры в пределах этой секции
            string xpath = ".//h3[@class='h5 mt-4']/a[@href] | " +  // Ссылки из <h3>
                           ".//li[@class='list-group-item']/h4/a[@href] | " +  // Ссылки из <li> и <h4>
                           ".//ul/li/a[@href]";  // Вложенные ссылки из <ul> и <li>

            // Выполняем поиск ссылок только внутри найденной секции
            var unitLinks = sectionNode.SelectNodes(xpath)?
                .Select(linkNode => linkNode.GetAttributeValue("href", ""))
                .Where(link => !string.IsNullOrEmpty(link))
                .Distinct()  // Убираем дубликаты
                .ToList();

            return unitLinks ?? new List<string>();  // Если ничего не найдено, возвращаем пустой список
        }

        private async Task ParseUnitEmployeesAndSaveNameUniversityIdEmailPhoneImg(string unitLink)
        {
            HttpClient httpClient = new HttpClient();
            var contacts = new List<PersonContact>();
            var web = new HtmlWeb();

            // Загружаем HTML-страницу с нужным блоком
            var doc = await Task.Run(() => web.Load($"https://www.smtu.ru{unitLink}#faculty-edu-content"));

            // Ищем карточки преподавателей по обновленному XPath
            var cardDivs = doc.DocumentNode.SelectNodes("//div[contains(@class, 'card') and contains(@class, 'text-bg-light')]");

            if (cardDivs != null)
            {
                foreach (var cardDiv in cardDivs)
                {
                    // Извлечение имени преподавателя (ссылка или текст)
                    var nameNode = cardDiv.SelectSingleNode(".//h4[@class='h6 text-info-dark']/a")
                         ?? cardDiv.SelectSingleNode(".//h4[@class='h6 text-info-dark']");

                    if (nameNode == null) continue;

                    var name = nameNode.InnerText.Trim();

                    // Извлечение email и телефона преподавателя
                    var person = new PersonContact
                    {
                        NameContact = name,
                        Email = cardDiv.SelectSingleNode(".//a[contains(@href, 'mailto:')]")?.InnerText.Trim(),
                        Telephone = cardDiv.SelectSingleNode(".//a[contains(@href, 'tel:')]")?.InnerText.Trim()
                    };

                    string id = "";
                    // Извлечение изображения преподавателя и ID из ссылки на изображение
                    var img = cardDiv.SelectSingleNode(".//img");

                    if (img != null)
                    {
                        var src = img.GetAttributeValue("src", "");
                        src = src.Split('?')[0]; // Убираем параметры запроса из URL

                        // Регулярное выражение для извлечения ID преподавателя из ссылки на изображение
                        var pattern = @"https://isu\.smtu\.ru/images/isu_person/small/p(\d+)\.jpg";
                        var idMatch = Regex.Match(src, pattern);

                        if (idMatch.Success)
                        {
                            id = idMatch.Groups[1].Value;
                            person.UniversityIdContact = id;

                            // Получение большого изображения
                            var bigImageUrl = src.Replace("small", "big");

                            try
                            {
                                HttpResponseMessage response = await httpClient.GetAsync(bigImageUrl);

                                // Проверка успешного статуса перед попыткой копирования содержимого
                                if (response.IsSuccessStatusCode)
                                {
                                    person.ImgPath = bigImageUrl;
                                }
                                else
                                {
                                    // Логируем статус ответа
                                    Console.WriteLine($"Failed to load large image: {bigImageUrl}. Status code: {response.StatusCode}");
                                    person.ImgPath = src; // Если большого изображения нет, сохраняем оригинал
                                }
                            }
                            catch (Exception ex)
                            {
                                // Обрабатываем ошибку загрузки изображения
                                Console.WriteLine($"Error while downloading image {bigImageUrl}: {ex.Message}");
                                person.ImgPath = src;  // Сохраняем оригинальный URL, если возникла ошибка
                            }
                        }
                        // Если изображения нет или ID не извлечен — пропускаем
                        else
                        {
                            continue;
                        }
                    }

                    // Сохранение информации в базе данных
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // Поиск существующего контакта по ID
                        var existingContact = await context.PersonContacts.FirstOrDefaultAsync(s => s.UniversityIdContact == id);

                        if (existingContact != null)
                        {
                            // Обновляем существующую запись
                            existingContact.NameContact = name;
                            existingContact.Email = person.Email;
                            existingContact.Telephone = person.Telephone;
                            existingContact.ImgPath = person.ImgPath;
                        }
                        else
                        {
                            // Добавляем нового преподавателя
                            await context.PersonContacts.AddAsync(person);
                        }

                        // Сохраняем изменения в базе данных
                        await context.SaveChangesAsync();
                    }
                }
            }
        }


        private async Task UpdatePersonContacts()
        {
            var web = new HtmlWeb();

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                foreach (var personContact in context.PersonContacts.ToList())
                {
                    var htmlDoc = await Task.Run(() => web.Load($"https://isu.smtu.ru/view_user_page/{personContact.UniversityIdContact}/"));

                    var mainNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='warper container-fluid']/div[@class='panel panel-default']");
                    if (mainNode == null) continue;

                    var positionNodes = mainNode.SelectNodes(".//div[@itemprop='post']/li");
                    personContact.Position = positionNodes?.Select(n => n.InnerText.Trim()).ToArray();

                    var taughtSubjectsNodes = mainNode.SelectNodes(".//div[@itemprop='teachingDiscipline']/li");
                    var taughtSubjects = taughtSubjectsNodes?.Select(n => n.InnerText.Trim()).ToList();

                    if (taughtSubjects != null)
                    {
                        foreach (var subjectName in taughtSubjects)
                        {
                            await AddOrUpdateSubject(context, personContact, subjectName);
                        }
                    }

                    var academicDegreeNode = mainNode.SelectSingleNode(".//div[@itemprop='degree']/li");
                    personContact.AcademicDegree = academicDegreeNode?.InnerText;

                    var teachingExperienceNode = mainNode.SelectSingleNode(".//div[@itemprop='specExperience']/li");
                    personContact.TeachingExperience = teachingExperienceNode?.InnerText;

                    await context.SaveChangesAsync();
                }
            }
        }

        private async Task AddOrUpdateSubject(ApplicationDbContext context, PersonContact personContact, string subjectName)
        {
            var existingSubject = await context.Subjects.FirstOrDefaultAsync(s => s.SubjectName == subjectName);

            if (existingSubject == null)
            {
                var subject = new Subject { SubjectName = subjectName };
                context.Subjects.Add(subject);
                await context.SaveChangesAsync();
            }

            var existingPersonTaughtSubject = await context.PersonTaughtSubjects.FirstOrDefaultAsync(pts => pts.IdContact == personContact.IdContact && pts.SubjectName == subjectName);

            if (existingPersonTaughtSubject == null)
            {
                var personTaughtSubject = new PersonTaughtSubject
                {
                    IdContact = personContact.IdContact,
                    SubjectName = subjectName
                };
                context.PersonTaughtSubjects.Add(personTaughtSubject);
                await context.SaveChangesAsync();
            }
        }
    }
}
