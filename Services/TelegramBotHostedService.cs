using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Protocol;
using static System.Net.Mime.MediaTypeNames;
using System.Configuration;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using MyMvcApp.Models;

using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using MyMvcApp.Hubs;
using System.IO.Compression;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Net.Http;



namespace MyMvcApp.Services
{
    public class TelegramBotHostedService : IHostedService
    {
      

        private readonly List<string> mySolutions = new List<string> { "Моя кармическая карма чиста, я в этом деле не виноват!🧞‍♂️", "Я всего лишь бездушная программа, чем могу помочь?🧞‍♂️",
            "Прошу прощения, я в отпуске на карантине от решения проблем. Попробуйте позже!🧞‍♂️", "Я всего лишь скромная программка, почи нять мировые проблемы не в моих компетенциях.🧞‍♂️",
            "Я испытываю трудности с нахождением решения вашей задачи. Может, попробуем что-то попроще?🧞‍♂️","В моем алгоритме не найдено подходящей функции для решения вашей проблемы. Давайте попробуем переформулировать вопрос?🧞‍♂️",
            "Моя цифровая магия сильна, но не настолько, чтобы решить эту задачу. Может, еще какой вопросик?🧞‍♂️","Кажется, это за пределами моих вычислительных способностей. Но я всегда готов помочь чем-то другим!🧞‍♂️"
        };

        private readonly TelegramBotClient _botClient;
        private readonly IConfiguration _configuration;
        private readonly List<string> _allowedUsernames;
        private CancellationTokenSource _cts;
        private readonly IHubContext<ChatHub> _hub;
        private readonly HttpClient _api;
        private readonly IHttpClientFactory _httpClientFactory; // Добавляем поле для фабрики

        public TelegramBotHostedService(string botToken, IConfiguration configuration,
            IHubContext<ChatHub> hub, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _botClient = new TelegramBotClient(botToken);
            _hub = hub;
            _httpClientFactory = httpClientFactory;
            _api = _httpClientFactory.CreateClient("UaiApi"); // Получаем именованный клиент
            _allowedUsernames = _configuration
                .GetSection("TelegramBotSettings:AllowedUsernames")
                .Get<List<string>>() ?? new List<string>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Удаляем все накопившиеся обновления.
            await ClearPendingUpdatesAsync(cancellationToken);

            // Запускаем асинхронный цикл обработки сообщений.
            _ = ReceiveMessagesAsync(0, _cts.Token);

            // Отключаем webhook (если используем long polling).
            await _botClient.DeleteWebhookAsync(cancellationToken: cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Отключаем webhook, чтобы освободить бота от старых настроек.
            await _botClient.DeleteWebhookAsync(cancellationToken: cancellationToken);

            // Завершаем цикл, отменяя токен.
            _cts.Cancel();
        }

        private async Task ClearPendingUpdatesAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Получаем все существующие обновления и сбрасываем их.
                var updates = await _botClient.GetUpdatesAsync(cancellationToken: cancellationToken);
                if (updates.Any())
                {
                    // Устанавливаем offset на последний update.Id + 1, чтобы сбросить старые обновления.
                    var lastUpdateId = updates.Last().Id;
                    await _botClient.GetUpdatesAsync(lastUpdateId + 1, cancellationToken: cancellationToken);
                    Console.WriteLine($"Очищено {updates.Length} старых обновлений.");
                }
                else
                {
                    Console.WriteLine("Нет накопившихся обновлений для очистки.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка очистки накопившихся обновлений: {ex.Message}");
            }
        }

        private async Task ReceiveMessagesAsync(int offset, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var updates = await _botClient.GetUpdatesAsync(offset, cancellationToken: cancellationToken);

                foreach (var update in updates)
                {
                    if (update.Type == UpdateType.Message)
                    {
                        var curMessage = update.Message;

                        if (_allowedUsernames.Contains(curMessage.From.Username))
                        {
                            await HandleUpdateMessageAsync(curMessage, cancellationToken);
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(curMessage.Chat.Id, "🚫 Я тебя не знаю! 🙅‍♂️", cancellationToken: cancellationToken);
                        }
                    }
                    else if (update.Type == UpdateType.CallbackQuery)
                    {
                        var curQuery = update.CallbackQuery;

                        if (_allowedUsernames.Contains(curQuery.From.Username))
                        {
                            await HandleUpdateCallbackQueryAsync(curQuery, cancellationToken);
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(curQuery.Message.Chat.Id, "🚫 Я тебя не знаю! 🙅‍♂️", cancellationToken: cancellationToken);
                        }
                    }
                    offset = update.Id + 1;
                }

            }
        }

        #region MessageHandler
        private async Task HandleUpdateMessageAsync(Message message, CancellationToken cancellationToken)
        {
            long chatId = message.Chat.Id;
            string username = message.From.Username;
            string messageText = message.Text;

            if (message.Type == MessageType.Document)
            {
                await CreateFileForBoardAsync(chatId, message.Document, cancellationToken);
                return;
            }

            string usernamePattern = @"(?<=^|\s)(?:@(?<name>[\w]+)|https://t\.me/(?<name>\w+))";
            Match usernameMatch = Regex.Match(messageText, usernamePattern);

            if (usernameMatch.Success)
            {
                string usernameValue = usernameMatch.Groups["name"].Value; // Используем именованную группу для извлечения нужной части
                GetAccesToUser(chatId, usernameValue, cancellationToken);
                return;
            }

            string pattern = @"^(\S+)\s+\[([^\[\]]+)\]\s+\[([^\[\]]+)\]";
            Match match = Regex.Match(messageText, pattern);

            if (match.Success)
            {
                string command = match.Groups[1].Value;
                string oldName = match.Groups[2].Value;
                string newName = match.Groups[3].Value;

                // Обработка команды /rename
                if (command == "/rename")
                {
                    // Вызов метода для переименования файла
                    await RenameFileAsync(chatId, oldName, newName);
                    return; // Прерываем выполнение метода, если была обработана команда /rename
                }

                if (command == "/add_faq")
                {
                    // Вызов метода для переименования файла
                    await AddFaqAsync(chatId, oldName, newName);
                    return; // Прерываем выполнение метода, если была обработана команда /rename
                }
            }

            switch (messageText)
            {
                //Первый этап
                case "Удалить документ ❌":
                    await AskDeleteFileFromBoardAsync(chatId, cancellationToken);
                    break;
                case "Добавить документ ✅":
                    await AskCreateFileForBoardAsync(chatId, cancellationToken);
                    break;
                case "Предоставить доступ 👥":
                    await AskGetAccesToUserAsync(chatId, cancellationToken);
                    break;
                case "Закрыть доступ 🚷":
                    await AskCloseUserAccesAsync(chatId, cancellationToken);
                    break;
                case "Переименовать документ ✏️":
                    await AskRenameFileForBoardAsync(chatId, cancellationToken);
                    break;
                case "Загрузить документ в AI🗂️":
                    await AskUploadDocListAsync(chatId, cancellationToken);   
                    break;
                case "Добавить FAQ ❓":
                    await AskAddFaqAsync(chatId, cancellationToken);
                    break;

                //Дефолт(Старт)
                case "Отмена":
                case "Стоп":
                case "отмена":
                case "стоп":
                default:
                    var keyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            new KeyboardButton("Добавить документ ✅"),
                            new KeyboardButton("Переименовать документ ✏️"),
                            new KeyboardButton("Удалить документ ❌")
                        },
                        new[]
                        {
                            new KeyboardButton("Предоставить доступ 👥"),
                            new KeyboardButton("Закрыть доступ 🚷")
                        },
                        new[]
                        {
                            new KeyboardButton("Загрузить документ в AI🗂️"),   // ← новая
                            new KeyboardButton("Добавить FAQ ❓")
                        }
                    });

                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Чего изволите?🧞‍♂️\n\n🤖 Чтобы отменить текущее действие напишите \"отмена\" или \"стоп\"",
                        replyMarkup: keyboard
                    );
                    break;
            }
        }

        private async Task AskDeleteFileFromBoardAsync(long chatId, CancellationToken cancellationToken)
        {
            string command = "delete";
            try
            {
                await SendDocumentListAsync(chatId, 1, command, cancellationToken);
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка запроса на удаление документа: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
        }

        private async Task AskRenameFileForBoardAsync(long chatId, CancellationToken cancellationToken)
        {
            string command = "rename";
            try
            {
                await SendDocumentListAsync(chatId, 1, command, cancellationToken);
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка запроса на переименование документа: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
        }

        private async Task AskCreateFileForBoardAsync(long chatId, CancellationToken cancellationToken)
        {
            await _botClient.SendTextMessageAsync(chatId, "🤖 Приложите документ/ы в сообщении", cancellationToken: cancellationToken);

        }

        private async Task GetAccesToUser(long chatId, string param, CancellationToken cancellationToken)
        {
            try
            {
                if (!_allowedUsernames.Contains(param))
                {
                    _allowedUsernames.Add(param);

                    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                    var configJson = System.IO.File.ReadAllText(configPath);
                    var configDoc = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(configJson);

                    var allowedUsernames = configDoc["TelegramBotSettings"]["AllowedUsernames"] as JArray;
                    if (allowedUsernames != null)
                    {
                        // Проверяем, содержит ли массив allowedUsernames значение param
                        if (!allowedUsernames.Contains(param))
                        {
                            // Добавляем новое значение в массив
                            allowedUsernames.Add(param);
                        }
                    }
                    var updatedConfigJson = Newtonsoft.Json.JsonConvert.SerializeObject(configDoc, Newtonsoft.Json.Formatting.Indented);
                    System.IO.File.WriteAllText(configPath, updatedConfigJson);

                    await _botClient.SendTextMessageAsync(chatId, $"✅ Пользователю @{param} успешно предаставлены права на использование бота");
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chatId, $"❌ Пользователь @{param} уже имеет права на использование бота");
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка предоставления пользователю прав: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
        }

        private async Task CloseAccesFromUser(long chatId, string param, CancellationToken cancellationToken)
        {
            try
            {
                if (_allowedUsernames.Contains(param))
                {
                    _allowedUsernames.Remove(param);

                    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                    var configJson = System.IO.File.ReadAllText(configPath);
                    var configDoc = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(configJson);

                    var allowedUsernames = configDoc["TelegramBotSettings"]["AllowedUsernames"];
                    if (allowedUsernames != null)
                    {
                        // Ищем индекс элемента, который нужно удалить
                        int indexToRemove = -1;
                        for (int i = 0; i < allowedUsernames.Count; i++)
                        {
                            if (allowedUsernames[i].ToString() == param)
                            {
                                indexToRemove = i;
                                break;
                            }
                        }

                        // Если нашли элемент, удаляем его
                        if (indexToRemove != -1)
                        {
                            allowedUsernames.RemoveAt(indexToRemove);
                        }
                    }
                    var updatedConfigJson = Newtonsoft.Json.JsonConvert.SerializeObject(configDoc, Newtonsoft.Json.Formatting.Indented);
                    System.IO.File.WriteAllText(configPath, updatedConfigJson);

                    await _botClient.SendTextMessageAsync(chatId, $"✅ Пользователь @{param} успешно удален из списка разрешенных");
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chatId, $"❌ Пользователь @{param} не найден в списке разрешенных");
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка удаления прав у пользователя: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
        }

        private async Task AskGetAccesToUserAsync(long chatId, CancellationToken cancellationToken)
        {
            await _botClient.SendTextMessageAsync(chatId, "🤖 Отправте мне контакт в сообщении", cancellationToken: cancellationToken);
        }

        private async Task AskCloseUserAccesAsync(long chatId, CancellationToken cancellationToken)
        {
            string command = "closeAccesUser";
            try
            {
                await SendAllowedUsersListAsync(chatId, 1, command, cancellationToken);
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка удаления прав у пользователя: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
            //await _botClient.SendTextMessageAsync(chatId, "🤖 Скоро можно будет забирать доступ у пользователя", cancellationToken: cancellationToken);
        }

        private async Task SendDocumentListAsync(long chatId, int pageNumber, string command, CancellationToken cancellationToken)
        {
            try
            {
                var directoryPath = Path.Combine("wwwroot", "documents-news-events", "documents");

                var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories).ToList();

                const int pageSize = 5;
                var startIndex = (pageNumber - 1) * pageSize;
                var endIndex = Math.Min(startIndex + pageSize, directories.Count);

                var directorySubset = directories.GetRange(startIndex, endIndex - startIndex);

                var buttons = new List<List<InlineKeyboardButton>>();

                foreach (var dir in directorySubset)
                {
                    string str = Path.GetFileName(dir);
                    StringBuilder com = new StringBuilder();
                    com.AppendFormat("{0}:{1}", command, Path.GetFileNameWithoutExtension(dir));

                    buttons.Add(new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData(str, com.ToString())
                    });
                }

                List<InlineKeyboardButton> padding = new List<InlineKeyboardButton>();

                if (pageNumber > 1)
                {
                    // Кодируем данные для кнопки "предыдущая страница"
                    StringBuilder com = new StringBuilder();
                    com.AppendFormat("previous-{1}:{0}", pageNumber - 1, command);
                    padding.Add(InlineKeyboardButton.WithCallbackData("<", com.ToString()));
                }

                StringBuilder info = new StringBuilder();
                info.AppendFormat("{0}/{1}", pageNumber, (int)Math.Ceiling((double)directories.Count / pageSize));
                padding.Add(InlineKeyboardButton.WithCallbackData(info.ToString(), " "));

                if (endIndex < directories.Count)
                {
                    StringBuilder com = new StringBuilder();
                    com.AppendFormat("next-{1}:{0}", pageNumber + 1, command);
                    padding.Add(InlineKeyboardButton.WithCallbackData(">", com.ToString()));
                }

                buttons.Add(padding);

                // Отправляем сообщение с инлайн кнопками
                await _botClient.SendTextMessageAsync(
                    chatId,
                    "🤖 Выберите документ:",
                    replyMarkup: new InlineKeyboardMarkup(buttons));
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка запроса документов: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
        }

        private async Task SendAllowedUsersListAsync(long chatId, int pageNumber, string command, CancellationToken cancellationToken)
        {
            try
            {
                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                var configJson = System.IO.File.ReadAllText(configPath);
                var configDoc = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(configJson);
                var allowedUsernames = configDoc["TelegramBotSettings"]["AllowedUsernames"] as JArray;

                if (allowedUsernames == null)
                {
                    throw new Exception("❌ Список разрешенных пользователей не найден.");
                }

                const int pageSize = 5;
                var startIndex = (pageNumber - 1) * pageSize;
                var endIndex = Math.Min(startIndex + pageSize, allowedUsernames.Count);

                var usernamesSubset = allowedUsernames.Skip(startIndex).Take(endIndex - startIndex);

                var buttons = new List<List<InlineKeyboardButton>>();

                foreach (var username in usernamesSubset)
                {
                    string displayName = username.ToString();
                    StringBuilder com = new StringBuilder();
                    com.AppendFormat("{0}:{1}", command, displayName);

                    buttons.Add(new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData(displayName, com.ToString())
                    });
                }

                List<InlineKeyboardButton> navigationButtons = new List<InlineKeyboardButton>();

                if (pageNumber > 1)
                {
                    StringBuilder com = new StringBuilder();
                    com.AppendFormat("previous-{1}:{0}", pageNumber - 1, command);
                    navigationButtons.Add(InlineKeyboardButton.WithCallbackData("<", com.ToString()));
                }

                StringBuilder info = new StringBuilder();
                info.AppendFormat("{0}/{1}", pageNumber, (int)Math.Ceiling((double)allowedUsernames.Count / pageSize));
                navigationButtons.Add(InlineKeyboardButton.WithCallbackData(info.ToString(), " "));

                if (endIndex < allowedUsernames.Count)
                {
                    StringBuilder com = new StringBuilder();
                    com.AppendFormat("next-{1}:{0}", pageNumber + 1, command);
                    navigationButtons.Add(InlineKeyboardButton.WithCallbackData(">", com.ToString()));
                }

                buttons.Add(navigationButtons);

                // Отправляем сообщение с инлайн кнопками
                await _botClient.SendTextMessageAsync(
                    chatId,
                    "🤖 Выберите контакт:",
                    replyMarkup: new InlineKeyboardMarkup(buttons));
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка запроса контактов: {ex.Message}");
            }
        }

        #endregion

        #region CallBackQueryHandler
        private async Task HandleUpdateCallbackQueryAsync(CallbackQuery query, CancellationToken cancellationToken)
        {
            long chatId = query.Message.Chat.Id;
            string username = query.From.Username;

            string sub_command = "";
            string command = query.Data.Split(":")[0];

            try
            {
                sub_command = command.Split("-")[1];
                command = command.Split("-")[0];
            }
            catch { }

            string param = query.Data.Split(":")[1];
            try
            {
                switch (command)
                {
                    //Первый этап
                    case "delete":
                        await DeleteFileFromBoardAsync(param, chatId, cancellationToken);
                        break;
                    case "previous":
                    case "next":
                        switch (sub_command)
                        {
                            case "closeAccesUser":
                                await SendAllowedUsersListAsync(chatId, int.Parse(param), sub_command, cancellationToken);
                                break;
                            default:
                                await SendDocumentListAsync(chatId, int.Parse(param), sub_command, cancellationToken);
                                break;
                        }
                        break;
                    case "rename":
                        await AskRenameFileFromBoardAsync(param, chatId, cancellationToken);
                        break;
                    case "closeAccesUser":
                        await CloseAccesFromUser(chatId, param, cancellationToken);
                        break;
                    case "getAccesUser":
                        await AskGetAccesToUserAsync(chatId, cancellationToken);
                        break;
                    case "upload_doc":           
                        await UploadDocAndSendAsync(param /* = FolderName */, chatId, cancellationToken);
                        break;

                }
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Неизвестная ошибка: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
        }

        private async Task DeleteFileFromBoardAsync(string param, long chatId, CancellationToken cancellationToken)
        {
            try
            {
                var directoryPath = Path.Combine("wwwroot", "documents-news-events", "documents");

                var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
                    .Where(dir => Path.GetFileNameWithoutExtension(dir).Contains(param))
                    .ToList();

                foreach (var directory in directories)
                {
                    try
                    {
                        // Удаляем папку
                        Directory.Delete(directory, true);
                        await _botClient.SendTextMessageAsync(chatId, $"✅ Документ \"{Path.GetFileName(directory)}\" был успешно удалён.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибки, возникшие при удалении папки
                        await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка удаления документа {Path.GetFileName(directory)}: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
                    }
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка при удалении документа: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
        }

        private async Task AskRenameFileFromBoardAsync(string param, long chatId, CancellationToken cancellationToken)
        {
            try
            {
                await _botClient.SendTextMessageAsync(
                      chatId,
                      $"<code>/rename [{param}] []</code> \n\n🤖 данную команду необходимо скопировать (просто кликнуть на неё) и вставить в поле ввода сообщения, во вторые скобки добавьте новое название файла.\n\nЗатем отправляйте мне🧞‍♂️",
                      parseMode: ParseMode.Html);
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка запроса на переименование документа: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
        }

        #endregion

        private async Task RenameFileAsync(long chatId, string oldName, string newName)
        {
            try
            {
                var mainDirectoryPath = Path.Combine("wwwroot", "documents-news-events", "documents");

                var directories = Directory.GetDirectories(mainDirectoryPath, $"{oldName}", SearchOption.AllDirectories).ToList();

                if (directories.Any())
                {
                    foreach (var directoryPath in directories)
                    {
                        try
                        {
                            string newDirectoryPath = Path.Combine(Path.GetDirectoryName(directoryPath), newName);

                            // Переименовываем папку
                            Directory.Move(directoryPath, newDirectoryPath);
                            await _botClient.SendTextMessageAsync(chatId, $"✅ Документ \"{Path.GetFileName(directoryPath)}\" успешно переименован в \"{Path.GetFileName(newDirectoryPath)}\"");
                        }
                        catch (Exception ex)
                        {
                            await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка переименования документа: {ex.Message}");
                        }
                    }
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chatId, $"Документ с именем \"{oldName}\" не найден.");
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка при переименовании документа: {ex.Message}");
            }
        }

        private async Task CreateFileForBoardAsync(long chatId, Telegram.Bot.Types.Document document, CancellationToken cancellationToken)
        {
            string extension = Path.GetExtension(document.FileName).ToLower();
            if (extension != ".pdf")
            {
                await _botClient.SendTextMessageAsync(chatId, "❌ Только PDF файлы поддерживаются.");
                return;
            }

            // Главная папка для сохранения документов
            string targetDirectory = Path.Combine("wwwroot", "documents-news-events", "documents");
            Directory.CreateDirectory(targetDirectory);

            // Имя файла и имя папки по названию документа
            string folderName = Path.GetFileNameWithoutExtension(document.FileName);
            string destinationFolder = Path.Combine(targetDirectory, folderName);
            string filePath = Path.Combine(targetDirectory, document.FileName);

            try
            {
                // Скачиваем PDF
                var fileInfo = await _botClient.GetFileAsync(document.FileId, cancellationToken);
                using (var stream = System.IO.File.Open(filePath, FileMode.Create))
                {
                    await _botClient.DownloadFile(fileInfo.FilePath, stream, cancellationToken);
                }

                // Создаём папку с именем документа
                Directory.CreateDirectory(destinationFolder);

                // Конвертируем PDF в изображения
                ConvertPdfToImages(filePath, destinationFolder);

                // Удаляем исходный PDF
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                await _botClient.SendTextMessageAsync(chatId, $"✅ Документ \"{Path.GetFileName(document.FileName)}\" был успешно преобразован.");
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка: {ex.Message}");
            }
        }

        static void ConvertPdfToImages(string sourcePath, string destDir)
        {
            // 1) Определяем, где лежит бинарник
            var popplerPath = System.IO.File.Exists("/usr/bin/pdftoppm")               // Linux-контейнер
                              ? "/usr/bin/pdftoppm"
                              : "pdftoppm";                                   // fallback (возможно в $PATH на Win)

            if (!System.IO.File.Exists(popplerPath) && popplerPath != "pdftoppm")
                throw new FileNotFoundException("pdftoppm not found in container");

            // 2) Гарантируем, что путь без пробелов обрамлён кавычками
            var src = $"\"{sourcePath}\"";
            var dst = $"\"{Path.Combine(destDir, "page")}\"";                // будет page-1.png, page-2.png …

            var psi = new ProcessStartInfo
            {
                FileName = popplerPath,
                Arguments = $"-png -r 300 {src} {dst}",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment =
        {
            ["LANG"] = "C.UTF-8"   // чтобы кириллица в пути не сломала poppler
        }
            };

            using var p = Process.Start(psi);
            string err = p!.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
                throw new Exception($"pdftoppm error ({p.ExitCode}): {err}");
        }

        static void RenameImages(string directoryPath)
        {
            var imageFiles = Directory.GetFiles(directoryPath, "page-*.png")
                                      .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Split('-').Last()));

            int pageIndex = 1;
            foreach (var filePath in imageFiles)
            {
                string newFileName = $"{pageIndex}.png";
                string newPath = Path.Combine(directoryPath, newFileName);

                System.IO.File.Move(filePath, newPath, overwrite: true);
                pageIndex++;
            }
        }

        private async Task UploadDocAndSendAsync(
    string folder,
    long chatId,
    CancellationToken ct)
        {
            // id соединения можно сделать уникальным для Телеграм-пользователя,
            // чтобы фронт фильтровал события именно для него
            var connId = $"tg_{chatId}";

            try
            {
                // ── подготовка пути ──────────────────────────────────────────────
                var root = Path.Combine("wwwroot", "documents-news-events", "documents");
                var folderPath = Path.Combine(root, folder);

                if (!Directory.Exists(folderPath))
                {
                    await _botClient.SendTextMessageAsync(
                        chatId,
                        "❌ Папка не найдена.",
                        cancellationToken: ct);
                    return;
                }

                // ── прогресс: старт ─────────────────────────────────────────────
                await _hub.Clients.All.SendAsync(
                    "DocProgress",
                    connId,
                    folder,
                    "start",
                    cancellationToken: ct);

                await _botClient.SendTextMessageAsync(
                    chatId,
                    "⏳ Архивирую…",
                    cancellationToken: ct);

                // ── 1. ZIP в память ─────────────────────────────────────────────
                await using var zipStream = new MemoryStream();
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in Directory.GetFiles(folderPath))
                    {
                        await using var fs = System.IO.File.OpenRead(file);
                        await using var zs = zip.CreateEntry(Path.GetFileName(file)).Open();
                        await fs.CopyToAsync(zs, ct);
                    }
                }
                zipStream.Position = 0;

                await _hub.Clients.All.SendAsync(
                    "DocProgress",
                    connId,
                    folder,
                    "zipped",
                    cancellationToken: ct);

                // ── 2. POST → FastAPI ───────────────────────────────────────────
                await _botClient.SendTextMessageAsync(
                    chatId,
                    "📤 Загружаю в AI…",
                    cancellationToken: ct);

                await _hub.Clients.All.SendAsync(
                    "DocProgress",
                    connId,
                    folder,
                    "uploading",
                    cancellationToken: ct);

                var mp = new MultipartFormDataContent
        {
            {
                new StreamContent(zipStream)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/zip") }
                },
                "file",
                $"{folder}.zip"
            }
        };

                _api.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:secret"))
                    );

                var resp = await _api.PostAsync("/api/v1/documents/upload", mp, ct);
                resp.EnsureSuccessStatusCode();

                // ── успех ───────────────────────────────────────────────────────
                await _hub.Clients.All.SendAsync(
                    "DocProgress",
                    connId,
                    folder,
                    "done",
                    cancellationToken: ct);

                await _botClient.SendTextMessageAsync(
                    chatId,
                    $"✅ Документ «{folder}» загружен!",
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                // ── ошибка ──────────────────────────────────────────────────────
                await _hub.Clients.All.SendAsync(
                    "DocProgress",
                    connId,
                    folder,
                    $"error:{ex.Message}",
                    cancellationToken: ct);

                await _botClient.SendTextMessageAsync(
                    chatId,
                    $"❌ Ошибка: {ex.Message}",
                    cancellationToken: ct);
            }
        }


        private async Task AskUploadDocListAsync(long chatId, CancellationToken ct)
        {
            var root = Path.Combine("wwwroot", "documents-news-events", "documents");
            var dirs = Directory.Exists(root)
                ? Directory.GetDirectories(root).OrderBy(Path.GetFileName).ToList()
                : new List<string>();

            if (!dirs.Any())
            {
                await _botClient.SendTextMessageAsync(chatId, "❌ Документы не найдены.", cancellationToken: ct);
                return;
            }

            var buttons = dirs.Select(d =>
                InlineKeyboardButton.WithCallbackData(
                    Path.GetFileNameWithoutExtension(d),          // текст на кнопке
                    $"upload_doc:{Path.GetFileName(d)}"           // callback-data
                )
            ).Select(b => new[] { b }).ToArray();                 // по одной кнопке в строке

            await _botClient.SendTextMessageAsync(
                chatId,
                "🤖 Выберите документ для отправки в AI-ассистент доски:",
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: ct);
        }

        private async Task AskAddFaqAsync(long chatId, CancellationToken ct)
        {
            const string tpl =
                "<b>Добавление FAQ-пары</b>\n\n" +
                "Скопируйте шаблон, впишите свой вопрос и краткий ответ:\n\n" +
                "<code>/add_faq [Как оформить академ?] [Обратитесь в деканат с заявлением]</code>\n\n" +
                "После отправки я передам данные в FAQ-сервис и сообщу результат ✅";

            await _botClient.SendTextMessageAsync(
                chatId,
                tpl,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }

        private async Task AddFaqAsync(long chatId, string q, string a)
        {
            try
            {
                var payload = new
                {
                    question = q,
                    answer = a
                };
                var jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Добавляем заголовок Basic Auth
                var byteArray = Encoding.ASCII.GetBytes("admin:secret");
                _api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                var response = await _api.PostAsync("/api/v1/faq/add", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseData = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    await _botClient.SendTextMessageAsync(chatId, $"✅ Запись FAQ успешно добавлена");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    await _botClient.SendTextMessageAsync(chatId, $"❌ Не удалось добавить запись FAQ: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка при добавлении записи FAQ: {ex.Message}\n\n{mySolutions[new Random().Next(0, mySolutions.Count())]}");
            }
        }

    }
}
