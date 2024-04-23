using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using VkNet;
using VkNet.Model;
using static System.Net.Mime.MediaTypeNames;



namespace MyMvcApp.Services
{
    public class NewsEventsDownloadService : IHostedService
    {

        private CancellationTokenSource _cts;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Запуск цикла в отдельной задаче
            _ = DownloadVkGroupData(_cts.Token);
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
                ParseAndSaveVkGroupData(Path.Combine("wwwroot", "documents-news-events"));
                await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
            }
        }

        private void ParseAndSaveVkGroupData(string dataFolderPath)
        {
            var api = new VkApi();

            api.Authorize(new ApiAuthParams
            {
                AccessToken = ""
            });

            var postsOVDFdit = ParseVkGroupData(api, "ovdsmtu",15);
            var postsFdit = ParseVkGroupData(api, "fditsmtu",5);

            var allPosts = postsFdit.Concat(postsOVDFdit).ToList();

            var jsonFilePath = Path.Combine(dataFolderPath, "vk_groups_info.json");

            File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(allPosts, Formatting.Indented));
        }

        public List<Models.VkPost> ParseVkGroupData(VkApi api, string screenName, int countPosts)
        {
            var groupId = api.Utils.ResolveScreenName(screenName).Id.Value;

            var posts = api.Wall.Get(new WallGetParams
            {
                OwnerId = -groupId, // Идентификатор группы должен быть отрицательным
                Count = (ulong)countPosts
            });

            var postInfos = new List<Models.VkPost>();

            foreach (var post in posts.WallPosts)
            {
                Post originalPost;

                if (post.CopyHistory != null && post.CopyHistory.Count > 0)
                {
                    originalPost = post.CopyHistory[0];
                }
                else
                {
                    originalPost = post;
                }

                List<string> imageUrls = new List<string>();

                if (originalPost.Attachments != null)
                {
                    foreach (var attachment in originalPost.Attachments)
                    {
                        if (attachment.Type == typeof(Photo))
                        {
                            imageUrls.Add(((Photo)attachment.Instance).Sizes.Last().Url.AbsoluteUri);
                        }
                        else if (attachment.Type == typeof(Video))
                        {
                            imageUrls.Add(((Video)attachment.Instance).Image.Last().Url.AbsoluteUri);
                        }
                        else if (attachment.Type == typeof(Album))
                        {
                            imageUrls.Add(((Album)attachment.Instance).Thumb.Sizes.Last().Url.AbsoluteUri);
                        }

                    }
                }


                string text = post.Text;
                // Регулярное выражение для поиска и замены вида [id32136|Ирины Тряскиной]
                string pattern = @"\[id(\d+)\|(.*?)\]";

                string replacement = "$2 (vk.com/id$1)";

                string output = Regex.Replace(text, pattern, replacement);

                postInfos.Add(new Models.VkPost
                {
                    Text = output,
                    ImageUrl = (imageUrls.Count > 0) ? imageUrls : new List<string> { "img/no_photo_post.png" },
                    Link = $"https://vk.com/wall{post.OwnerId}_{post.Id}",
                    DatePost = post.Date.GetValueOrDefault()
                });


            }

            return postInfos.Distinct().ToList();
        }


    }
}