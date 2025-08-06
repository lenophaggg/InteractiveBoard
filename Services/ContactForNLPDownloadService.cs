using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyMvcApp.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyMvcApp.Services
{
    public class ContactForNLPDownloadService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ContactForNLPDownloadService> _logger;
        private CancellationTokenSource _cts;
        private Task _executingTask;

        public ContactForNLPDownloadService(
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory,
            ILogger<ContactForNLPDownloadService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
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
            await RunContactDownload(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                DateTime nextRunTime = now.Date.AddDays(1).AddHours(4);
                TimeSpan delay = nextRunTime - now;

                _logger.LogInformation("Следующий запуск сервиса ContactForNLPDownloadService запланирован на {NextRunTime}, через {Seconds} секунд", nextRunTime, delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break; // Остановка по требованию
                }

                await RunContactDownload(cancellationToken);
            }
        }

        private async Task RunContactDownload(CancellationToken cancellationToken)
        {
            try
            {
                await SendPersonContactsToApi();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в процессе отправки данных PersonContacts в API");
            }
        }

        private async Task SendPersonContactsToApi()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var personContacts = await context.PersonContacts
                    .Include(pc => pc.TaughtSubjects)
                    .ToListAsync();

                var personContactDtos = new List<PersonContactDto>();

                foreach (var contact in personContacts)
                {
                    var taughtGroups = await context.ScheduleData
                        .Where(s => s.InstructorId == contact.IdContact && s.Group != null)
                        .Select(s => s.Group!)
                        .Distinct()
                        .ToListAsync();

                    var dto = new PersonContactDto
                    {
                        IdContact = contact.IdContact,
                        UniversityIdContact = contact.UniversityIdContact,
                        NameContact = contact.NameContact,
                        Position = contact.Position ?? Array.Empty<string>(),
                        AcademicDegree = contact.AcademicDegree ?? string.Empty,
                        TeachingExperience = contact.TeachingExperience ?? string.Empty,
                        Telephone = contact.Telephone ?? string.Empty,
                        Email = contact.Email ?? string.Empty,
                        Information = contact.Information ?? string.Empty,
                        ImgPath = contact.ImgPath ?? string.Empty,
                        TaughtSubjects = contact.TaughtSubjects?.Select(ts => ts.SubjectName).Distinct().ToList() ?? new List<string>(),
                        TaughtGroups = taughtGroups
                    };

                    personContactDtos.Add(dto);
                }

                var client = _httpClientFactory.CreateClient("UaiApi");
                var json = JsonConvert.SerializeObject(personContactDtos);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync("/api/v1/persons/upload", content);
                    response.EnsureSuccessStatusCode();
                    _logger.LogInformation("✔️ Данные PersonContacts успешно отправлены в API");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "❌ Ошибка при отправке данных в API");
                }

            }
        }
    }
}