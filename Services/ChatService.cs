using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyMvcApp.Services
{
    public class ChatService
    {
        private readonly HttpClient _http;

        public ChatService(HttpClient http)
        {
            _http = http;
        }

        public async Task<string> ProcessMessageAsync(string connectionId, string message)
        {
            try
            {
                // 1) HTTP-запрос
                var resp = await _http.GetAsync($"search?q={Uri.EscapeDataString(message)}");
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    // пытаемся прочитать поле "detail"
                    string detail;
                    try
                    {
                        using var errDoc = JsonDocument.Parse(body);
                        detail = errDoc.RootElement.GetProperty("detail").GetString()!;
                    }
                    catch
                    {
                        detail = body;
                    }

                    return $@"
                        <p class=""error-message"">
                            <strong>{System.Net.WebUtility.HtmlEncode(detail)}!</strong>
                        </p>";
                }

                // 2) Разбор успешного JSON
                using var doc = JsonDocument.Parse(body);
                var results = doc.RootElement.GetProperty("results");
                if (results.GetArrayLength() == 0)
                {
                    return @"<p>Ничего не найдено. Попробуйте уточнить запрос.</p>";
                }

                // 3) Берём первое совпадение
                var first = results[0];
                var id = first.GetProperty("university_idcontact").GetString();
                var name = first.GetProperty("namecontact").GetString();

                // 4) Список первых трёх позиций
                var positions = first.GetProperty("position")
                                     .EnumerateArray()
                                     .Select((j, i) => new { j, i })
                                     .Where(x => x.i < 3)
                                     .Select(x => x.j.GetString()!)
                                     .ToList();

                // 5) Собираем HTML внутри одного .message
                var sb = new StringBuilder();

                // Заголовок
                sb.Append($@"<strong class=""contact-name"">{System.Net.WebUtility.HtmlEncode(name)}</strong>");

                // Позиции
                sb.Append(@"<ul class=""contact-positions"">");
                foreach (var pos in positions)
                {
                    sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(pos)}</li>");
                }
                sb.Append("</ul>");

                // Эскейпим одинарные кавычки в name/id для JS
                var jsName = name.Replace("'", "\\'");

                sb.Append($@"
                    <button class=""details-btn""
                            onclick=""openModalInfo('{jsName}','{id}')"">
                        Подробнее
                    </button>");

                return sb.ToString();
            }
            catch (HttpRequestException ex)
            {
                return $@"
                    <p class=""error-message"">
                        Сетевая ошибка: <strong>{System.Net.WebUtility.HtmlEncode(ex.Message)}</strong>
                    </p>";
            }
            catch (Exception ex)
            {
                return $@"
                    <p class=""error-message"">
                        Ошибка: <strong>{System.Net.WebUtility.HtmlEncode(ex.Message)}</strong>
                    </p>";
            }
        }
    }
}
