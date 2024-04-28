namespace MyMvcApp.Services
{
    public class DocumentCleanupService : IHostedService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);
        private static readonly TimeSpan FileAgeLimit = TimeSpan.FromDays(7);
        private static readonly string DocumentsPath = Path.Combine("wwwroot", "documents-news-events", "documents");
        private CancellationTokenSource _cts;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Запуск цикла в отдельной задаче
            _ = CleanupOldFilesPeriodically(_cts.Token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();

            return Task.CompletedTask;
        }

        private async Task CleanupOldFilesPeriodically(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                CleanupOldFiles();
                await Task.Delay(CheckInterval, cancellationToken);
            }
        }

        private void CleanupOldFiles()
        {
            var directories = Directory.GetDirectories(DocumentsPath);

            foreach (var directory in directories)
            {
                var directoryInfo = new DirectoryInfo(directory);

                if (DateTime.Now - directoryInfo.LastWriteTime > FileAgeLimit)
                {
                    directoryInfo.Delete(true); // true для рекурсивного удаления
                }
            }
        }
    }
}