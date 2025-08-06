using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MyMvcApp.Models;

namespace MyMvcApp.Services
{
    /// <summary>
    /// Сервис для обращения к API и получения структурированного ответа.
    /// JWT-токен добавляется в заголовок каждого запроса здесь, без изменения Program.cs.
    /// </summary>
    public class ChatService
    {
        private readonly HttpClient _http;
        private const string _testToken = "test-token-123";

        public ChatService(HttpClient http)
        {
            _http = http;
        }

        /// <summary>
        /// Отправляет сообщение в API и возвращает десериализованный ApiResponse.
        /// В случае ошибки возвращает ApiResponse с сообщением об ошибке.
        /// </summary>
        /// <param name="userId">Идентификатор пользователя или соединения.</param>
        /// <param name="text">Текст сообщения.</param>
        public async Task<ApiResponse> ProcessMessageAsync(string userId, string text)
        {
            var requestObj = new
            {
                id = Guid.NewGuid().ToString(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { query = text },
                meta = new { source = "unified", user = userId }
            };

            var json = JsonSerializer.Serialize(requestObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Формируем HttpRequestMessage, чтобы добавить заголовок авторизации здесь
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/query")
            {
                Content = content
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _testToken);

            try
            {
                using var resp = await _http.SendAsync(requestMessage);
                resp.EnsureSuccessStatusCode();

                var body = await resp.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return apiResponse ?? new ApiResponse();
            }
            catch (HttpRequestException httpEx)
            {
                return new ApiResponse
                {
                    Answers = Array.Empty<string>(),
                    Message = "Ошибка при обращении к API"
                };
            }
            catch (JsonException jsonEx)
            {
                return new ApiResponse
                {
                    Answers = Array.Empty<string>(),
                    Message = $"Невозможно обработать ответ API: {jsonEx.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Answers = Array.Empty<string>(),
                    Message = $"Произошла ошибка: {ex.Message}"
                };
            }
        }
    }
}
