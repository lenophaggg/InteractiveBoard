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

using iTextSharp.text;
using iTextSharp.text.pdf;
using Spire.Pdf;
using Patagames.Pdf;
using Patagames.Pdf.Net; // Подключаем библиотеку Patagames.Pdf.Net
using Patagames.Pdf.Enums;

using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Xceed.Words.NET;

using System.Xml.XPath;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using MyMvcApp.Models;
using static System.Net.WebRequestMethods;
using System.Drawing.Imaging;
using System.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MyMvcApp.Services
{
    public class TelegramBotHostedService : IHostedService
    {
        private readonly TelegramBotClient _botClient;
        private CancellationTokenSource _cts;
        private readonly IConfiguration _configuration;

        private readonly List<string> _allowedUsernames;

        private readonly List<string> mySolutions = new List<string> { "Моя кармическая карма чиста, я в этом деле не виноват!🧞‍♂️", "Я всего лишь бездушная программа, чем могу помочь?🧞‍♂️",
            "Прошу прощения, я в отпуске на карантине от решения проблем. Попробуйте позже!🧞‍♂️", "Я всего лишь скромная программка, почи нять мировые проблемы не в моих компетенциях.🧞‍♂️",
            "Я испытываю трудности с нахождением решения вашей задачи. Может, попробуем что-то попроще?🧞‍♂️","В моем алгоритме не найдено подходящей функции для решения вашей проблемы. Давайте попробуем переформулировать вопрос?🧞‍♂️",
            "Моя цифровая магия сильна, но не настолько, чтобы решить эту задачу. Может, еще какой вопросик?🧞‍♂️","Кажется, это за пределами моих вычислительных способностей. Но я всегда готов помочь чем-то другим!🧞‍♂️"
        };


        public TelegramBotHostedService(string botToken, IConfiguration _configuration)
        {
            _botClient = new TelegramBotClient(botToken);
            _configuration = _configuration;
            _allowedUsernames = _configuration.GetSection("TelegramBotSettings:AllowedUsernames").Get<List<string>>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Запуск цикла в отдельной задаче
            _ = ReceiveMessagesAsync(0, _cts.Token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();

            return Task.CompletedTask;
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
            var extension = Path.GetExtension(document.FileName).ToLower();
            if (extension != ".pdf" && extension != ".docx" && extension != ".doc")
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Формат файла \"{document.FileName}\" не поддерживается. Поддерживаются только файлы PDF и Word.");
                return;
            }

            var fileId = document.FileId;

            var tempFilePath = Path.Combine("wwwroot", "documents-news-events", "documents", document.FileName);

            var directoryName = Path.GetFileNameWithoutExtension(document.FileName); // Имя папки будет без расширения файла
            var directoryPath = Path.Combine("wwwroot", "documents-news-events", "documents", directoryName);

            if (Directory.Exists(directoryPath))
            {
                await _botClient.SendTextMessageAsync(chatId, $"✅ Документ с именем \"{directoryName}\" уже существует.");
                return;
            }
            else
            {
                Directory.CreateDirectory(directoryPath);
            }

            var file = await _botClient.GetFileAsync(fileId);

            try
            {
                using (var saveImageStream = System.IO.File.Open(tempFilePath, FileMode.Create))
                {
                    await _botClient.DownloadFileAsync(file.FilePath, saveImageStream, cancellationToken);
                }

                var savedExtension = Path.GetExtension(tempFilePath).ToLower();
                if (savedExtension == ".pdf")
                {
                    SavePdfToImg(tempFilePath, Path.Combine("wwwroot", "documents-news-events", "documents", Path.GetFileNameWithoutExtension(document.FileName)));

                    System.IO.File.Delete(tempFilePath);
                }
                else
                {
                    string newSourcePath = Path.Combine(Path.GetDirectoryName(tempFilePath), $"{Path.GetFileNameWithoutExtension(tempFilePath)}.pdf");

                    ConvertDocToPdf(tempFilePath, newSourcePath);
                    SavePdfToImg(newSourcePath, Path.Combine("wwwroot", "documents-news-events", "documents", Path.GetFileNameWithoutExtension(document.FileName)));

                    System.IO.File.Delete(newSourcePath);
                }

                await _botClient.SendTextMessageAsync(chatId, $"✅ Документ \"{Path.GetFileName(document.FileName)}\" успешно сохранен");
            }
            catch (Exception ex)
            {
                Directory.Delete(Path.Combine("wwwroot", "documents-news-events", "documents", Path.GetFileNameWithoutExtension(document.FileName)), true);
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка сохранения документа \"{Path.GetFileName(document.FileName)}\": {ex.Message}");
            }
        }

        static void ConvertDocToPdf(string docFilePath, string pdfFilePath)
        {
            // Открываем DOCX файл с помощью библиотеки DocX
            using (var document = DocX.Load(docFilePath))
            {
                // Создаем новый PDF документ
                var pdf = new PdfSharpCore.Pdf.PdfDocument();
                PdfSharpCore.Pdf.PdfPage page = pdf.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);

                // Настраиваем шрифт и другие параметры
                XFont font = new XFont("Arial", 12, XFontStyle.Regular);
                double yPosition = 20;

                // Читаем каждый параграф из Word документа и добавляем его в PDF
                foreach (var paragraph in document.Paragraphs)
                {
                    gfx.DrawString(paragraph.Text, font, XBrushes.Black,
                        new XRect(20, yPosition, page.Width - 40, page.Height - 40),
                        XStringFormats.TopLeft);
                    yPosition += 20; // Переход на следующую строку

                    // Добавляем новую страницу, если место на текущей странице закончилось
                    if (yPosition > page.Height - 40)
                    {
                        page = pdf.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        yPosition = 20;
                    }
                }

                // Обрабатываем таблицы
                foreach (var table in document.Tables)
                {
                    double tableYPosition = yPosition;
                    double tableXPosition = 20;
                    double columnWidth = (page.Width - 40) / table.ColumnCount; // ширина каждой колонки
                    double rowHeight = 20; // высота строки

                    // Рисуем таблицу
                    foreach (var row in table.Rows)
                    {
                        double rowYPosition = tableYPosition;
                        tableXPosition = 20;

                        foreach (var cell in row.Cells)
                        {
                            // Рисуем границы ячеек
                            gfx.DrawRectangle(XPens.Black, tableXPosition, rowYPosition, columnWidth, rowHeight);

                            // Рисуем текст в ячейках
                            gfx.DrawString(cell.Paragraphs[0].Text, font, XBrushes.Black,
                                new XRect(tableXPosition + 2, rowYPosition + 2, columnWidth - 4, rowHeight - 4),
                                XStringFormats.TopLeft);

                            tableXPosition += columnWidth; // Переход к следующей колонке
                        }

                        tableYPosition += rowHeight; // Переход к следующей строке

                        // Добавляем новую страницу, если место на текущей странице закончилось
                        if (tableYPosition + rowHeight > page.Height - 40)
                        {
                            page = pdf.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            tableYPosition = 20;
                        }
                    }
                }

                // Обрабатываем изображения
                foreach (var image in document.Images)
                {
                    using (var imageStream = image.GetStream(FileMode.Open, FileAccess.Read))
                    {
                        var pdfImage = XImage.FromStream(() => imageStream);

                        gfx.DrawImage(pdfImage, 20, yPosition, pdfImage.PixelWidth, pdfImage.PixelHeight);
                        yPosition += pdfImage.PixelHeight + 10; // Переход на следующую строку

                        // Добавляем новую страницу, если место на текущей странице закончилось
                        if (yPosition > page.Height - 40)
                        {
                            page = pdf.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            yPosition = 20;
                        }
                    }
                }

                // Сохраняем PDF документ
                using (FileStream stream = new FileStream(pdfFilePath, FileMode.Create, FileAccess.Write))
                {
                    pdf.Save(stream);
                }
            }
        }

        static void SavePdfToImg(string sourcePath, string destinationFolder)
        {
            int pagesPerPart = 3; // Количество страниц на каждую часть
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            using (var document = Patagames.Pdf.Net.PdfDocument.Load(sourcePath))
            {
                int totalPages = document.Pages.Count; // Получаем количество страниц в документе

                // Цикл по каждой странице и конвертация в изображение
                for (int page = 0; page < totalPages; page++)
                {
                    // Получаем размер страницы
                    var pdfPage = document.Pages[page];
                    int width = (int)(pdfPage.Width * 2);  // Увеличиваем разрешение для рендеринга (например, в 2 раза)
                    int height = (int)(pdfPage.Height * 2);

                    // Рендерим страницу с заданными параметрами (300 DPI)
                    using (var image = new Bitmap(width, height))
                    {
                        using (var graphics = Graphics.FromImage(image))
                        {
                            // Устанавливаем фон на белый (PDF-страницы могут иметь прозрачный фон)
                            graphics.Clear(System.Drawing.Color.White);

                            // Получаем HDC из Graphics
                            IntPtr hdc = graphics.GetHdc();
                            try
                            {
                                // Рендерим страницу на HDC
                                pdfPage.Render(hdc, 0, 0, width, height, PageRotate.Normal, RenderFlags.FPDF_LCD_TEXT);
                            }
                            finally
                            {
                                // Освобождаем HDC
                                graphics.ReleaseHdc(hdc);
                            }

                            // Сохраняем изображение
                            string outputPath = Path.Combine(destinationFolder, $"Page_{page + 1}.png");
                            image.Save(outputPath, ImageFormat.Png);

                        }
                    }
                }
            }
        }


    }
}
