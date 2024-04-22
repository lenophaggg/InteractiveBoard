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

        public TypeWeekDownloadService(IConfiguration configuration, IOptionsMonitor<ScheduleOptions> scheduleOptions)
        {
            _configuration = configuration;
            _scheduleOptions = scheduleOptions;
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

                var web = new HtmlWeb();
                var doc = web.Load("https://www.smtu.ru/ru/listschedule/");

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