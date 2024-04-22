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
using System.Xml.XPath;

namespace MyMvcApp.Services
{
    public class TelegramBotHostedService : IHostedService
    {
        private readonly TelegramBotClient _botClient;
        private CancellationTokenSource _cts;

        private readonly List<string> _allowedUsernames;

        private readonly List<string> mySolutions = new List<string> { "Моя кармическая карма чиста, я в этом деле не виноват!🧞‍♂️", "Я всего лишь бездушная программа, чем могу помочь?🧞‍♂️",
            "Прошу прощения, я в отпуске на карантине от решения проблем. Попробуйте позже!🧞‍♂️", "Я всего лишь скромная программка, почи нять мировые проблемы не в моих компетенциях.🧞‍♂️",
            "Я испытываю трудности с нахождением решения вашей задачи. Может, попробуем что-то попроще?🧞‍♂️","В моем алгоритме не найдено подходящей функции для решения вашей проблемы. Давайте попробуем переформулировать вопрос?🧞‍♂️",
            "Моя цифровая магия сильна, но не настолько, чтобы решить эту задачу. Может, еще какой вопросик?🧞‍♂️","Кажется, это за пределами моих вычислительных способностей. Но я всегда готов помочь чем-то другим!🧞‍♂️"
        };


        public TelegramBotHostedService(string botToken, IConfiguration _configuration)
        {
            _botClient = new TelegramBotClient(botToken);

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
        private async Task AskGetAccesToUserAsync(long chatId, CancellationToken cancellationToken)
        {
            await _botClient.SendTextMessageAsync(chatId, "🤖 Скоро можно будет давать доступ пользователю", cancellationToken: cancellationToken);

        }
        private async Task AskCloseUserAccesAsync(long chatId, CancellationToken cancellationToken)
        {
            await _botClient.SendTextMessageAsync(chatId, "🤖 Скоро можно будет забирать доступ у пользователя", cancellationToken: cancellationToken);

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
                        await SendDocumentListAsync(chatId, int.Parse(param), sub_command, cancellationToken);
                        break;
                    case "rename":
                        await AskRenameFileFromBoardAsync(param, chatId, cancellationToken);
                        break;
                    case "Закрыть доступ 🚷":
                        await AskCloseUserAccesAsync(chatId, cancellationToken);
                        break;
                    case "Переименовать документ ✏️":
                        await AskRenameFileForBoardAsync(chatId, cancellationToken);
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
                    await _botClient.SendTextMessageAsync(chatId, $"Папка с именем \"{oldName}\" не найдена.");
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка при переименовании документа: {ex.Message}");
            }
        }



        private async Task CreateFileForBoardAsync(long chatId, Telegram.Bot.Types.Document document, CancellationToken cancellationToken)
        {
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

            var extension = Path.GetExtension(document.FileName).ToLower();
            if (extension != ".pdf" && extension != ".docx" && extension != ".doc")
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Формат файла \"{document.FileName}\" не поддерживается. Поддерживаются только файлы PDF и Word.");
                return;
            }
            else
            {
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
                    Directory.Delete(Path.Combine("wwwroot", "documents-news-events", "documents", Path.GetFileNameWithoutExtension(document.FileName)),true);
                    await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка сохранения документа \"{Path.GetFileName(document.FileName)}\": {ex.Message}");
                }
            }
        }

        static void ConvertDocToPdf(string sourcePath, string newSourcePath)
        {
            Spire.Doc.Document document = new Spire.Doc.Document();

            document.LoadFromFile(sourcePath);

            document.SaveToFile(newSourcePath, Spire.Doc.FileFormat.PDF);

            System.IO.File.Delete(sourcePath);
        }

        static void SavePdfToImg(string sourcePath, string destinationFolder)
        {
            int pagesPerPart = 3; // Количество страниц на каждую часть
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            using (PdfReader reader = new PdfReader(sourcePath))
            {
                int totalPages = reader.NumberOfPages;
                int parts = (int)Math.Ceiling((double)totalPages / pagesPerPart);

                for (int i = 0; i < parts; i++)
                {
                    int startPage = i * pagesPerPart + 1;
                    int endPage = Math.Min(startPage + pagesPerPart - 1, totalPages);

                    // Используем MemoryStream вместо FileStream
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (iTextSharp.text.Document document = new iTextSharp.text.Document())
                        using (PdfCopy copy = new PdfCopy(document, ms))
                        {
                            document.Open();
                            for (int page = startPage; page <= endPage; page++)
                            {
                                PdfImportedPage importedPage = copy.GetImportedPage(reader, page);
                                copy.AddPage(importedPage);
                            }
                            document.Close();
                        }

                        // Получаем массив байтов из MemoryStream
                        byte[] pdfBytes = ms.ToArray();

                        // Преобразуем байты в MemoryStream
                        using (MemoryStream partStream = new MemoryStream(pdfBytes))
                        {
                            ConvertPdfToImages(partStream, destinationFolder, i + 1);
                        }
                    }
                }
            }
        }


        public static void ConvertPdfToImages(MemoryStream pdfStream, string outputFolder, int partNumber)
        {
            // Загрузка PDF-документа из MemoryStream
            Spire.Pdf.PdfDocument doc = new Spire.Pdf.PdfDocument(pdfStream);

            // Создаем папку для сохранения изображений, если она не существует
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // Преобразование каждой страницы в изображение
            for (int i = 0; i < doc.Pages.Count; i++)
            {
                // Извлекаем изображение из страницы PDF
                System.Drawing.Image image = doc.SaveAsImage(i);

                // Формируем имя файла для каждого изображения
                string outputPath = Path.Combine(outputFolder, $"Part_{partNumber}_Page_{i + 1}.png");

                // Сохраняем изображение в файл
                image.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);

                // Освобождаем ресурсы используемого изображения
                image.Dispose();
            }

            // Освобождение ресурсов, связанных с PDF-документом
            doc.Close();
        }

    }
}