using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using System.Diagnostics;


namespace MyMvcApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly ApplicationDbContext _context;


    public HomeController(ILogger<HomeController> logger, IWebHostEnvironment hostingEnvironment, ApplicationDbContext context)
    {
        _logger = logger;
        _hostingEnvironment = hostingEnvironment;
        _context = context;
    }

    public IActionResult Index()
    {
        var dataParser = new DataParserModel();

        // Грузим все посты из всех групп и объединяем в один список
        string jsonPath = Path.Combine(_hostingEnvironment.WebRootPath, "documents-news-events/vk_groups_info.json");
        var groupDict = dataParser.LoadVkGroupData(jsonPath);

        var allPosts = groupDict.Values
            .SelectMany(list => list)
            .OrderByDescending(p => p.DatePost)
            .ToList();

        var documents = dataParser.LoadFilesFromDocumentsFolder(_hostingEnvironment.WebRootPath);

        // --- Masonry-раскладка по рядам, не по колонкам! ---
        int columns = 3; // сколько столбцов надо
        var masonryColumns = Enumerable.Range(0, columns)
            .Select(_ => new List<VkPost>())
            .ToList();

        for (int i = 0; i < allPosts.Count; i++)
        {
            masonryColumns[i % columns].Add(allPosts[i]);
        }

        // Передаем masonryColumns в View вместо списка!
        return View((documents, masonryColumns));
    }

    [HttpGet]
    public IActionResult GetDocument(string directoryPath, string directoryName)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
                return NotFound("Директория не найдена.");

            var imageFiles = Directory.GetFiles(directoryPath, "*.png")
                                      .OrderBy(f =>
                                      {
                                          var name = Path.GetFileNameWithoutExtension(f);
                                          return int.TryParse(name, out var p) ? p : int.MaxValue;
                                      })
                                      .ToArray();

            var bytesList = imageFiles
                            .Select(System.IO.File.ReadAllBytes)
                            .ToList();

            var model = new PdfDocumentViewModel
            {
                ImageBytesList = bytesList,
                DirectoryName = directoryName
            };

            // Указываем полный путь с расширением, чтобы Razor точно нашёл файл
            return PartialView(
                "~/Views/Shared/_PDFDocument.cshtml",
                model
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при формировании PDF-документа");
            return StatusCode(500, "Произошла ошибка при обработке запроса.");
        }
    }

    // Метод GetPostData
    [HttpGet]
    public IActionResult GetPostData(int postId)
    {
        var dataParser = new DataParserModel();

        // Загрузка данных о постах
        var posts = dataParser.LoadVkPostData(Path.Combine(_hostingEnvironment.WebRootPath, "documents-news-events/vk_groups_info.json"));

        // Поиск поста по ID
        var post = posts.FirstOrDefault(p => p.Id == postId);

        if (post == null)
        {
            return NotFound();
        }

        // Очистка ModelState для корректной передачи данных в представление
        ModelState.Clear();

        return PartialView("_InfoFromMasonryGridItem", post);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
