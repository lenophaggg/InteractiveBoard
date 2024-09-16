using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using VkNet;
using VkNet.Model;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyMvcApp.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Diagnostics;

namespace MyMvcApp.Services
{
    public class NewsEventsDownloadService : IHostedService
    {
        private static TimeSpan CheckInterval = TimeSpan.FromMinutes(30);
        private CancellationTokenSource _cts;
        private readonly IServiceProvider _serviceProvider;
        private VkApi _vkApi;

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

            // Сортировка постов по дате публикации, от новых к старым
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

            return posts.WallPosts.Select(post => CreateVkPost(post)).ToList();
        }

        private VkPost CreateVkPost(Post post)
        {
            var originalPost = post.CopyHistory?.FirstOrDefault() ?? post;
            var imageUrls = originalPost.Attachments?
                .Where(a => a.Instance != null)
                .SelectMany(a => ExtractImageUrls(a))
                .ToList();

            if (imageUrls == null || imageUrls.Count == 0)
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

        private IEnumerable<string> ExtractImageUrls(Attachment attachment)
        {
            switch (attachment.Type.Name)
            {
                case "Photo":
                    Photo photo = (Photo)attachment.Instance;
                    if (photo.Sizes.Any())
                        return new List<string> { photo.Sizes.LastOrDefault()?.Url.AbsoluteUri };
                    break;
                case "Video":

                    string vkVideoUrl = "https://vk.com/video-177798967_456239646";

                    string ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp", "yt-dlp.exe");
                    string arguments = $"--get-url {vkVideoUrl}";

                    Process process = new Process();
                    process.StartInfo.FileName = ytDlpPath;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    // Запуск процесса
                    process.Start();

                    // Чтение результата (прямая ссылка на видео)
                    string videoUrl = process.StandardOutput.ReadToEnd();

                    // Ожидание завершения процесса
                    process.WaitForExit();

                    // Возвращаем полученную ссылку
                    return new List<string> { videoUrl.Trim() };

                    break;
                case "Album":
                    Album album = (Album)attachment.Instance;
                    if (album.Thumb.Sizes.Any())
                        return new List<string> { album.Thumb.Sizes.LastOrDefault()?.Url.AbsoluteUri };
                    break;
            }
            return Enumerable.Empty<string>();
        }

        private string GetDirectVideoUrl(string iframeUrl)
        {
            var options = new ChromeOptions();
            options.AddArgument("headless"); // Запуск в headless-режиме, без графического интерфейса

            using (IWebDriver driver = new ChromeDriver(options))
            {
                driver.Navigate().GoToUrl(iframeUrl);

                try
                {
                    // Ожидание загрузки страницы и выполнения всех скриптов
                    WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                    wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));

                    // Попытка найти элемент <video>
                    var videoElement = wait.Until(d => d.FindElement(By.TagName("video")));
                    string videoUrl = videoElement.GetAttribute("src");

                    // Проверяем, что URL не пустой
                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        return videoUrl; // Возвращаем прямую ссылку на видео
                    }
                    else
                    {
                        // Если URL пустой, попробуем найти другие ссылки
                        return FindVideoLinkInPage(driver);
                    }
                }
                catch (NoSuchElementException)
                {
                    // Если элемент <video> не найден, попробуем найти другие ресурсы на странице
                    return FindVideoLinkInPage(driver);
                }
            }
        }

        private string FindVideoLinkInPage(IWebDriver driver)
        {
            try
            {
                // Поиск всех ссылок на странице
                var allLinks = driver.FindElements(By.TagName("a"));

                foreach (var link in allLinks)
                {
                    string href = link.GetAttribute("href");

                    // Проверяем, есть ли ссылка на видеофайл
                    if (!string.IsNullOrEmpty(href) && (href.Contains(".mp4") || href.Contains("video")))
                    {
                        return href; // Возвращаем первую найденную ссылку на видео
                    }
                }

                return "Прямая ссылка на видео не найдена";
            }
            catch (Exception ex)
            {
                return $"Ошибка при поиске видеоссылок: {ex.Message}";
            }
        }
    }
}
