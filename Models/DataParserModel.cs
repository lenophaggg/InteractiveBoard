using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NuGet.DependencyResolver;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    public class DataParserModel
    {
        public Dictionary<string, List<VkPost>> LoadVkGroupData(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                // Если файл не найден, возвращаем пустой словарь
                return new Dictionary<string, List<VkPost>>();
            }

            string jsonData = System.IO.File.ReadAllText(filePath);

            try
            {
                // Десериализация JSON в словарь с группами и их постами
                var data = JsonConvert.DeserializeObject<Dictionary<string, List<VkPost>>>(jsonData);

                if (data == null)
                {
                    // Если десериализация вернула null, возвращаем пустой словарь
                    return new Dictionary<string, List<VkPost>>();
                }

                // Сортируем посты каждой группы по дате
                foreach (var group in data.Keys.ToList())
                {
                    data[group] = data[group]
                        .OrderByDescending(post => post.DatePost)
                        .ToList();
                }

                return data;
            }
            catch (JsonReaderException ex)
            {
                Console.WriteLine($"Ошибка при чтении JSON файла: {ex.Message}");
                Console.WriteLine($"Путь к ошибке: {ex.Path}, строка: {ex.LineNumber}, позиция: {ex.LinePosition}");

                // Возвращаем пустой словарь для продолжения работы
                return new Dictionary<string, List<VkPost>>();
            }
        }

        public List<VkPost> LoadVkPostData(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                // Если файл не найден, возвращаем пустой список
                return new List<VkPost>();
            }

            try
            {
                // Загрузка данных из JSON файла
                string jsonData = System.IO.File.ReadAllText(filePath);

                // Десериализация JSON в Dictionary<string, List<VkPost>>
                var allGroupsPosts = JsonConvert.DeserializeObject<Dictionary<string, List<VkPost>>>(jsonData)
                                     ?? new Dictionary<string, List<VkPost>>();

                // Объединяем все списки постов в один
                var posts = allGroupsPosts.Values.SelectMany(groupPosts => groupPosts).ToList();

                // Сортировка постов по дате
                return posts.OrderByDescending(post => post.DatePost).ToList();
            }
            catch (JsonException ex)
            {
                // Логируем ошибку и возвращаем пустой список
                Console.WriteLine($"Ошибка при чтении JSON: {ex.Message}");
                return new List<VkPost>();
            }
        }

        public List<Document> LoadFilesFromDocumentsFolder(string documentsFolderPath)
        {
            string documentsPath = Path.Combine(documentsFolderPath, "documents-news-events", "documents");
            List<Document> documents = new List<Document>();

            string[] directories = Directory.GetDirectories(documentsPath);


            foreach (string directory in directories)
            {
                Document document = new Document
                {
                    DocumentName = Path.GetFileName(directory).Replace("_", " "),
                    DocumentPath = directory
                };
                documents.Add(document);
            }

            return documents;
        }
    }

    public class ScheduleOptions
    {
        public string TypeWeek { get; set; }
        // Другие свойства, если они есть
    }

    #region LoadingSchedules
    [PrimaryKey("ScheduleId")]
    [Table("schedules")]
    public class ScheduleData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("scheduleid")]
        public int ScheduleId { get; set; } // Первичный ключ

        [Column("dayofweek")]
        public string? DayOfWeek { get; set; } // День недели

        [Column("starttime")]
        public TimeSpan? StartTime { get; set; } // Время начала

        [Column("endtime")]
        public TimeSpan? EndTime { get; set; } // Время конца

        [Column("weektype")]
        public string? WeekType { get; set; } // Тип недели (верхняя, нижняя, обе)

        [Column("classroomnumber")]
        public string? Classroom { get; set; } // Аудитория
        [ForeignKey("Classroom")]
        public Classrooms ClassroomNumber { get; set; }

        [Column("groupnumber")]
        public string? Group { get; set; } // Группа
        [ForeignKey("Group")]
        public Groups GroupNumber { get; set; }

        [Column("subjectname")]
        public string? Subject { get; set; }
        [ForeignKey("Subject")]
        public Subject SubjectName { get; set; }
        
        [Column("instructorid")]     
        public int? InstructorId { get; set; }
        [ForeignKey("InstructorId")]
        public PersonContact Instructor { get; set; }

        [Column("scheduleinfo")]
        public string? ScheduleInfo { get; set; }
    }

    public class CurrentSchedule
    {
        public string Group { get; set; }
        public string Subject { get; set; }
        public string ScheduleInfo { get; set; }
        public string Classroom { get; set; }
        public string InstructorName { get; set; }
        public string Status { get; set; }

    }

    #endregion

    [PrimaryKey("SubjectName")]
    [Table("subjects")]
    public class Subject
    {
        [Column("subjectname")]
        public string SubjectName { get; set; }       
    }

    [PrimaryKey("IdContact", "SubjectName")]
    [Table("person_taughtsubjects")]
    public class PersonTaughtSubject
    {
        [Column("idcontact")]
        public int IdContact { get; set; }
        [Column("subjectname")]
        public string SubjectName { get; set; }

        [ForeignKey("IdContact")]
        public PersonContact Person { get; set; }

        [ForeignKey("SubjectName")]
        public Subject Subject { get; set; }
    }

    [PrimaryKey("Name")]
    [Table("faculties")]
    public class Faculties
    {
        [Column("name")]
        public string Name { get; set; }
    }

    [PrimaryKey("Number")]
    [Table("groups")]
    public class Groups
    {
        [Column("groupnumber")]
        public string Number { get; set; }
        [Column("facultyname")]
        public string? FacultyName { get; set; }
        [ForeignKey("FacultyName")]
        public Faculties? Faculty { get; set; }
    }

    [Table("actual_groups")]
    [PrimaryKey("GroupNumber")]
    public class ActualGroup
    {
        [Column("groupnumber")]
        public string GroupNumber { get; set; }
    }

    [PrimaryKey("ClassroomNumber")]
    [Table("classrooms")]
    public class Classrooms
    {
        [Column("classroomnumber")]
        public string ClassroomNumber { get; set; }
    }

    #region LoadingVk

    public class VkPost
    {
        public long Id { get; set; }           // Уникальный идентификатор поста
        public string Text { get; set; }        // Текст поста
        public List<string> ImageUrl { get; set; }  // URL изображений
        public List<int> ImageHeight { get; set; }  // Высота изображений для ширины 646px
        public List<string> VideoUrl { get; set; }  // URL видео
        public List<int> VideoHeight { get; set; }  // Высота видео для ширины 646px
        public DateTime DatePost { get; set; }
        public string Link { get; set; }
    }


    #endregion
}


