using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using MyMvcApp.Models;

namespace MyMvcApp.Services
{
    public class TypeWeekDownloadService : IHostedService
    {
        private static TimeSpan CheckInterval = TimeSpan.FromHours(6);
        private CancellationTokenSource _cts;
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<ScheduleOptions> _scheduleOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TypeWeekDownloadService> _logger;

        public TypeWeekDownloadService(
            IConfiguration configuration,
            IOptionsMonitor<ScheduleOptions> scheduleOptions,
            IHttpClientFactory httpClientFactory,
            ILogger<TypeWeekDownloadService> logger)
        {
            _configuration = configuration;
            _scheduleOptions = scheduleOptions;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Запуск цикла в отдельной задаче
            _ = DownloadTypeWeek(_cts.Token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();

            return Task.CompletedTask;
        }

        private async Task DownloadTypeWeek(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {

                var client = _httpClientFactory.CreateClient("Smtu");
                HttpResponseMessage response;

                try
                {
                    response = await client.GetAsync("ru/listschedule/", cancellationToken);
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Ошибка при загрузке страницы https://www.smtu.ru/ru/listschedule/");
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    continue;
                }

                var htmlContent = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                string xpath = "//h4[contains(.,'Сегодня')]";

                HtmlNode node = doc.DocumentNode.SelectSingleNode(xpath);
                string text = node.InnerText;
                string[] parts = text.Split(',');
                string value = parts[2].Trim();// Преобразование первого символа в верхний регистр
                value = char.ToUpper(value[0]) + value.Substring(1); // "нижняя неделя"

                // Обновляем значение в ScheduleOptions
                _scheduleOptions.CurrentValue.TypeWeek = value;

                // Сохраняем изменения в appsettings.json
                _configuration["ScheduleOptions:TypeWeek"] = value;
                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                var configJson = File.ReadAllText(configPath);
                var configDoc = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(configJson);
                configDoc["ScheduleOptions"]["TypeWeek"] = value;
                var updatedConfigJson = Newtonsoft.Json.JsonConvert.SerializeObject(configDoc, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configPath, updatedConfigJson);

                await Task.Delay(CheckInterval, cancellationToken);
            }
        }

    }
}