using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;
using VkNet;
using VkNet.Model;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyMvcApp.Models;

namespace MyMvcApp.Services
{
    public class NewsEventsDownloadService : IHostedService
    {
        private static TimeSpan CheckInterval = TimeSpan.FromMinutes(30);
        private CancellationTokenSource _cts;
        private readonly IServiceProvider _serviceProvider;
        private VkApi _vkApi;
        private readonly string ytDlpPath = @"C:\Users\user\Desktop\papka\MyMvcApp\yt-dlp\yt-dlp.exe";  // путь к yt-dlp

        public NewsEventsDownloadService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _vkApi = new VkApi();
            _vkApi.Authorize(new ApiAuthParams
            {
                AccessToken = "21a09de121a09de121a09de15722b6c098221a021a09de144fb01384c67302821e770c3"
            });
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = DownloadVkGroupData(_cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            return Task.CompletedTask;
        }

        private async Task DownloadVkGroupData(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await ParseAndSaveVkGroupData(Path.Combine("wwwroot", "documents-news-events"), cancellationToken);
                await Task.Delay(CheckInterval, cancellationToken);
            }
        }

        private async Task ParseAndSaveVkGroupData(string dataFolderPath, CancellationToken cancellationToken)
        {
            var tasks = new List<Task<List<VkPost>>>
            {
                ParseVkGroupDataAsync(_vkApi, "ovdsmtu", 15),
                ParseVkGroupDataAsync(_vkApi, "fditsmtu", 5),
                ParseVkGroupDataAsync(_vkApi, "stipendiasmtu", 5)
            };

            var results = await Task.WhenAll(tasks);
            var allPosts = results.SelectMany(x => x).ToList();

            var prioritizedPosts = allPosts.OrderByDescending(post => post.DatePost).ToList();

            var jsonFilePath = Path.Combine(dataFolderPath, "vk_groups_info.json");

            using (var writer = new StreamWriter(jsonFilePath))
            {
                string json = JsonConvert.SerializeObject(prioritizedPosts, Formatting.Indented);
                await writer.WriteAsync(json);
            }
        }

        public async Task<List<VkPost>> ParseVkGroupDataAsync(VkApi api, string screenName, int countPosts)
        {
            var groupId = (await api.Utils.ResolveScreenNameAsync(screenName)).Id.Value;
            var posts = await api.Wall.GetAsync(new WallGetParams
            {
                OwnerId = -groupId,
                Count = (ulong)countPosts
            });

            return await Task.WhenAll(posts.WallPosts.Select(post => CreateVkPostAsync(post)));
        }

        private async Task<VkPost> CreateVkPostAsync(Post post)
        {
            var originalPost = post.CopyHistory?.FirstOrDefault() ?? post;

            // Обрабатываем вложения асинхронно
            var imageUrlsTasks = originalPost.Attachments?
                .Where(a => a.Instance != null)
                .Select(a => ExtractImageUrlsOrDownloadVideo(a))
                ?? Enumerable.Empty<Task<IEnumerable<string>>>();

            var imageUrlsResults = await Task.WhenAll(imageUrlsTasks);

            // Объединяем результаты из всех задач
            var imageUrls = imageUrlsResults.SelectMany(urls => urls).ToList();

            if (imageUrls.Count == 0)
            {
                imageUrls = new List<string> { "/img/no_photo_post.png" };
            }

            string pattern = @"\[id(\d+)\|(.*?)\]";
            string replacement = "$2 (vk.com/id$1)";
            string output = Regex.Replace(post.Text, pattern, replacement);

            return new VkPost
            {
                Text = output,
                ImageUrl = imageUrls,
                Link = $"https://vk.com/wall{post.OwnerId}_{post.Id}",
                DatePost = post.Date.GetValueOrDefault()
            };
        }

        private async Task<IEnumerable<string>> ExtractImageUrlsOrDownloadVideo(Attachment attachment)
        {
            switch (attachment.Type.Name)
            {
                case "Photo":
                    Photo photo = (Photo)attachment.Instance;
                    if (photo.Sizes.Any())
                        return new List<string> { photo.Sizes.LastOrDefault()?.Url.AbsoluteUri };
                    break;
                case "Video":
                    Video video = (Video)attachment.Instance;
                    if (video.Player != null)
                    {
                        var videoUrl = video.Player.AbsoluteUri; // Преобразуем Uri в строку
                        var savedPath = await DownloadVideoWithYtDlpAsync(videoUrl); // Асинхронная загрузка видео
                        return new List<string> { savedPath };
                    }
                    break;
                case "Album":
                    Album album = (Album)attachment.Instance;
                    if (album.Thumb.Sizes.Any())
                        return new List<string> { album.Thumb.Sizes.LastOrDefault()?.Url.AbsoluteUri };
                    break;
            }
            return Enumerable.Empty<string>();
        }

        private async Task<string> DownloadVideoWithYtDlpAsync(string videoUrl)
        {
            // Путь для сохранения видео
            var saveFolderPath = Path.Combine("wwwroot", "videos");
            if (!Directory.Exists(saveFolderPath))
            {
                Directory.CreateDirectory(saveFolderPath);
            }

            var videoFileName = $"{Guid.NewGuid()}.mp4"; // уникальное имя файла
            var saveFilePath = Path.Combine(saveFolderPath, videoFileName);

            // Запуск yt-dlp для загрузки видео
            var processInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = $"\"{videoUrl}\" -o \"{saveFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                process.Start();

                // Асинхронное ожидание завершения процесса
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Ошибка при загрузке видео: {error}");
                }
            }

            return saveFilePath; // Возвращаем путь к скачанному видео
        }
    }
}