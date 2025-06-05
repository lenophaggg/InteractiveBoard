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
            // Проверяем, существует ли директория
            if (!Directory.Exists(directoryPath))
            {
                return NotFound("Директория не найдена.");
            }

            // Получаем все файлы изображений в папке, сортируем их по имени (числовой порядок)
            string[] imageFiles = Directory.GetFiles(directoryPath, "*.png")
                                           .OrderBy(f =>
                                           {
                                               string fileName = Path.GetFileNameWithoutExtension(f);
                                               return int.TryParse(fileName, out int pageNumber) ? pageNumber : int.MaxValue;
                                           })
                                           .ToArray();

            // Загружаем каждое изображение в список в виде байтовых массивов
            List<byte[]> imageBytesList = imageFiles.Select(file => System.IO.File.ReadAllBytes(file)).ToList();

            // Возвращаем частичное представление с передачей списка байтовых массивов изображений и имени директории
            return PartialView("_PDFDocument", (imageBytesList, directoryName));
        }
        catch (Exception ex)
        {
            // Логируем ошибку и возвращаем 500
            Console.WriteLine($"Ошибка: {ex.Message}");
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
