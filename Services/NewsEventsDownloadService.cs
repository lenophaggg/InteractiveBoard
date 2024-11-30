using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using VkNet;
using VkNet.Model;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyMvcApp.Models;
using System.Diagnostics;
using System.Threading;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MyMvcApp.Services
{
    public class NewsEventsDownloadService : IHostedService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(90);
        private CancellationTokenSource _cts;
        private readonly IServiceProvider _serviceProvider;
        private readonly VkApi _vkApi;
        private readonly ILogger<NewsEventsDownloadService> _logger;

        private readonly string _videosOutputDirectory = Path.Combine("wwwroot", "documents-news-events", "videos");

        public NewsEventsDownloadService(IServiceProvider serviceProvider, ILogger<NewsEventsDownloadService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            _vkApi = new VkApi();
            _vkApi.Authorize(new ApiAuthParams
            {
                AccessToken = "21a09de121a09de121a09de15722b6c098221a021a09de144fb01384c67302821e770c3"
            });

            if (!Directory.Exists(_videosOutputDirectory))
            {
                Directory.CreateDirectory(_videosOutputDirectory);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NewsEventsDownloadService is starting.");
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = DownloadVkGroupData(_cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NewsEventsDownloadService is stopping.");
            _cts.Cancel();
            return Task.CompletedTask;
        }

        private async Task DownloadVkGroupData(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting VK group data download...");
                    await ParseAndSaveVkGroupData(Path.Combine("wwwroot", "documents-news-events"), cancellationToken);
                    _logger.LogInformation("VK group data download completed.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during VK group data download.");
                }
                await Task.Delay(CheckInterval, cancellationToken);
            }
        }

        private async Task ParseAndSaveVkGroupData(string dataFolderPath, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Parsing VK group data...");

                var jsonFilePath = Path.Combine(dataFolderPath, "vk_groups_info.json");

                // Load existing data from JSON
                var allGroupsPosts = File.Exists(jsonFilePath)
                    ? JsonConvert.DeserializeObject<Dictionary<string, List<VkPost>>>(await File.ReadAllTextAsync(jsonFilePath))
                      ?? new Dictionary<string, List<VkPost>>()
                    : new Dictionary<string, List<VkPost>>();

                var groupConfigs = new List<(string ScreenName, int MaxPosts)>
        {
            ("ovdsmtu", 15),
            ("fditsmtu", 7),
            ("stipendiasmtu", 5)
        };

                foreach (var (screenName, maxPosts) in groupConfigs)
                {
                    var vkPosts = await ParseVkGroupDataAsync(_vkApi, screenName, maxPosts);

                    if (!allGroupsPosts.ContainsKey(screenName))
                    {
                        allGroupsPosts[screenName] = new List<VkPost>();
                    }

                    var existingPosts = allGroupsPosts[screenName];

                    // Remove duplicates and add new posts
                    var newPosts = vkPosts.Where(post => !existingPosts.Any(ep => ep.Id == post.Id)).ToList();
                    existingPosts.AddRange(newPosts);

                    // Sort posts by date descending
                    var updatedPosts = existingPosts.OrderByDescending(post => post.DatePost).ToList();

                    // Determine posts to keep and posts to remove
                    var postsToKeep = updatedPosts.Take(maxPosts).ToList();
                    var postsToRemove = updatedPosts.Skip(maxPosts).ToList();

                    // Update the posts list
                    allGroupsPosts[screenName] = postsToKeep;

                    // Delete videos associated with removed posts
                    foreach (var post in postsToRemove)
                    {
                        if (post.VideoUrl != null)
                        {
                            foreach (var videoUrl in post.VideoUrl)
                            {
                                // Convert videoUrl to file path
                                var videoFileName = Path.GetFileName(videoUrl);
                                var videoFilePath = Path.Combine(_videosOutputDirectory, videoFileName);

                                if (File.Exists(videoFilePath))
                                {
                                    try
                                    {
                                        File.Delete(videoFilePath);
                                        _logger.LogInformation($"Deleted video file: {videoFilePath}");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, $"Error deleting video file: {videoFilePath}");
                                    }
                                }
                            }
                        }
                    }

                    if (newPosts.Any())
                    {
                        _logger.LogInformation($"Updated posts for group {screenName}.");
                    }
                    else
                    {
                        _logger.LogInformation($"No new posts for group {screenName}, but outdated posts were removed if any.");
                    }
                }

                // Save updated data to JSON
                await File.WriteAllTextAsync(jsonFilePath, JsonConvert.SerializeObject(allGroupsPosts, Formatting.Indented));
                _logger.LogInformation("All group data successfully saved.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while parsing and saving VK group data.");
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

            var postTasks = posts.WallPosts.Select(post => CreateVkPostAsync(post));
            var vkPosts = await Task.WhenAll(postTasks);

            return vkPosts.ToList();
        }

        private async Task<VkPost> CreateVkPostAsync(Post post)
        {
            var originalPost = post.CopyHistory?.FirstOrDefault() ?? post;

            // Extract image URLs and heights from Photo attachments
            var imageUrls = originalPost.Attachments?
                .Where(a => a.Type.Name == "Photo")
                .SelectMany(ExtractImageUrls)
                .ToList();

            var imageHeights = originalPost.Attachments?
                .Where(a => a.Type.Name == "Photo")
                .SelectMany(ExtractImageHeights)
                .ToList();

            // Replace mentions in the text
            string pattern = @"\[id(\d+)\|(.*?)\]";
            string replacement = "$2 (vk.com/id$1)";
            string output = Regex.Replace(post.Text, pattern, replacement);

            // Extract and download videos
            var videoUrls = await ExtractAndDownloadVideos(originalPost.Attachments);
            var videoHeights = videoUrls.Select(_ => 360).ToList();

            // If no images are found, use a default image
            if ((imageUrls == null || imageUrls.Count == 0) && (videoUrls == null || videoUrls.Count == 0))
            {
                imageUrls = new List<string> { "/img/no_photo_post.png" };
                imageHeights = new List<int> { 360 };
            }

            return new VkPost
            {
                Id = post.Id ?? 0,
                Text = output,
                ImageUrl = imageUrls,
                ImageHeight = imageHeights,
                VideoUrl = videoUrls,
                VideoHeight = videoHeights,
                Link = $"https://vk.com/wall{post.OwnerId}_{post.Id}",
                DatePost = post.Date.GetValueOrDefault()
            };
        }

        private IEnumerable<int> ExtractImageHeights(Attachment attachment)
        {
            if (attachment.Type.Name == "Photo" && attachment.Instance is Photo photo && photo.Sizes.Any())
            {
                var originalSize = photo.Sizes.LastOrDefault();
                if (originalSize != null)
                {
                    int originalWidth = (int)originalSize.Width;
                    int originalHeight = (int)originalSize.Height;
                    int calculatedHeight = (int)(646.0 / originalWidth * originalHeight);
                    return new List<int> { calculatedHeight };
                }
            }
            return new List<int> { 360 };
        }

        private IEnumerable<string> ExtractImageUrls(Attachment attachment)
        {
            if (attachment.Type.Name == "Photo" && attachment.Instance is Photo photo && photo.Sizes.Any())
            {
                return new List<string> { photo.Sizes.LastOrDefault()?.Url.ToString() };
            }
            return Enumerable.Empty<string>();
        }

        private async Task<List<string>> ExtractAndDownloadVideos(IEnumerable<Attachment> attachments)
        {
            var videoUrls = new List<string>();

            foreach (var attachment in attachments.Where(a => a.Type.Name == "Video"))
            {
                var video = (Video)attachment.Instance;

                // Construct the video URL
                if (video.Id.HasValue && video.OwnerId.HasValue)
                {
                    string videoUrl = $"https://vk.com/video{video.OwnerId}_{video.Id}";

                    // Download the video
                    var localVideoPath = await DownloadVideoAsync(videoUrl);
                    if (!string.IsNullOrEmpty(localVideoPath))
                    {
                        // Return the relative path to the video for use in the view
                        videoUrls.Add(localVideoPath);
                    }
                }
            }

            return videoUrls;
        }

        private async Task<string> DownloadVideoAsync(string videoUrl)
        {
            try
            {
                var videoFileName = $"{Guid.NewGuid()}.mp4";
                var outputPath = Path.Combine(_videosOutputDirectory, videoFileName);

                // Ensure the output directory exists
                if (!Directory.Exists(_videosOutputDirectory))
                {
                    Directory.CreateDirectory(_videosOutputDirectory);
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--output \"{outputPath}\" --quiet --no-warnings --download-sections \"*0-90\" \"{videoUrl}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                    }
                }

                if (File.Exists(outputPath))
                {
                    // Return the path relative to the web root for referencing in the HTML
                    var relativePath = $"/documents-news-events/videos/{videoFileName}";
                    return relativePath;
                }
                else
                {
                    _logger.LogWarning($"Video download failed for URL: {videoUrl}");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading video from URL: {videoUrl}");
                return string.Empty;
            }
        }
    }
}
