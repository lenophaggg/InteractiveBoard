using System.Linq;
using System.Net;
using System.Runtime.Intrinsics.X86;
using System.Text;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Wordprocessing;
using MyMvcApp.Models;
using VkNet.Model;

namespace MyMvcApp.Services
{
    public class ChatFormatter
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public ChatFormatter(ApplicationDbContext db, IWebHostEnvironment hostingEnvironment)  // DbContext is injected
        {
            _db = db;
            _hostingEnvironment = hostingEnvironment;
        }

        public string ToHtml(ApiResponse resp)
        {
            var sb = new StringBuilder();


            if (!string.IsNullOrEmpty(resp.Message))
            {
                sb.Append($"<div class=\"bot-message-hint\">{WebUtility.HtmlEncode(resp.Message)}</div>");
            }

            // В зависимости от источника формируем вывод
            switch (resp.Source)
            {
                case "person":
                    var idList = resp.Answers
                                     .Where(id => !string.IsNullOrEmpty(id))
                                     .ToList();

                    var contacts = _db.PersonContacts
                        .Where(pc => idList.Contains(pc.NameContact))
                        .ToList();

                    sb.Append("<div class=\"contact-list\">");
                    foreach (var person in contacts)
                    {


                        sb.Append($@"
                            <div style=""margin-top:0.5rem;"">
                                {person.NameContact}
                                <a href=""javascript:void(0);"" 
                                   class=""btn-contact""
                                   onclick=""openModalInfo('{person.NameContact}', '{person.IdContact}')"">
                                    Подробнее
                                </a>
                            </div>
                        ");
                    }
                    sb.Append("</div>");
                    break;

                case "doc":
                    var docNames = resp.Answers
                                      .Where(path => !string.IsNullOrEmpty(path))
                                      .ToList();

                    // виртуальная папка, относительно wwwroot
                    var virtualDir = "documents-news-events/documents";

                    sb.Append("<div class=\"document-list\">");

                    foreach (var rawName in docNames)
                    {
                        var absPath = // тут твой полный путь, как ты делаешь сейчас
                            Path.Combine(_hostingEnvironment.WebRootPath, "documents-news-events", "documents", Path.GetFileNameWithoutExtension(rawName));
                        // Если нужен с расширением, используй Path.GetFileName(rawName)

                        var absPathForJS = absPath.Replace("\\", "\\\\").Replace("'", "\\'");

                        var displayName = Path.GetFileNameWithoutExtension(rawName);

                        sb.Append($@"
<div style=""margin-top:0.5rem;"">
    <a href=""#"" 
       class=""btn-doc doc-link"" 
       onclick=""openModalPDF('{absPathForJS}', '{displayName}')"">
        {WebUtility.HtmlEncode(rawName)}
    </a>
</div>
");
                    }
                    sb.Append("</div>");
                    break;


                default:
                    sb.Append("<div class=\"answer-list\">");
                    for (int i = 0; i < resp.Answers.Length; i++)
                    {
                        sb.Append($"<div class=\"bot-message\">{WebUtility.HtmlEncode(resp.Answers[i])}</div>");
                        if (i < resp.Answers.Length - 1)
                            sb.Append("<div class=\"answer-separator\"></div>");
                    }
                    sb.Append("</div>");
                    break;
            }

            return sb.ToString();
        }

        public string ToTelegramText(ApiResponse resp)
        {
            var sb = new StringBuilder();
            foreach (var ans in resp.Answers)
                sb.AppendLine(ans);
            if (!string.IsNullOrEmpty(resp.Message))
                sb.AppendLine($"\n_{resp.Message}_");
            return sb.ToString();
        }
    }
}
