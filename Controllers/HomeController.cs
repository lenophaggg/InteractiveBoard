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

        List<VkPost> vkInfoDataList =
            dataParser.LoadDataFromJson<VkPost>(Path.Combine(_hostingEnvironment.WebRootPath, "documents-news-events/vk_groups_info.json"));
        // Передача модели в представление                  
       
        List<Document> documents = dataParser.LoadFilesFromDocumentsFolder(_hostingEnvironment.WebRootPath);
        return View((documents, vkInfoDataList));
    }

    [HttpGet]
    public IActionResult GetDocument(string directoryPath, string directoryName)
    {
        List<byte[]> imageBytesList = new List<byte[]>();

        // Формируем полный путь к папке

        // Проверяем, существует ли директория
        if (Directory.Exists(directoryPath))
        {
            // Получаем все файлы изображений в папке
            string[] imageFiles = Directory.GetFiles(directoryPath, "*.png");

            // Загружаем каждое изображение в список в виде байтовых массивов
            foreach (string imageFile in imageFiles)
            {
                byte[] imageBytes = System.IO.File.ReadAllBytes(imageFile);
                imageBytesList.Add(imageBytes);
            }
        }

        // Возвращаем частичное представление с передачей списка байтовых массивов изображений и имени директории
        return PartialView("_PDFDocument", (imageBytesList, directoryName));
    }

    // Метод GetPostData
    [HttpGet]
    public IActionResult GetPostData(string postId)
    {
        var dataParser = new DataParserModel();
        // Загрузка данных из .json файла
        List<VkPost> posts = dataParser.LoadDataFromJson<VkPost>(Path.Combine(_hostingEnvironment.WebRootPath, "documents-news-events/vk_groups_info.json"));

        // Находим пост с указанным id
        var post = posts.FirstOrDefault(p => p.Link == postId);
        if (post == null)
        {
            return NotFound();
        }

        // Очистите ModelState от предыдущих ошибок
        ModelState.Clear();

        return PartialView("_InfoFromMasonryGridItem", post);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
