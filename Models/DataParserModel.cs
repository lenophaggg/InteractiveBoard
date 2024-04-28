using Newtonsoft.Json;

namespace MyMvcApp.Models
{
    public class DataParserModel
    {
        public List<T> LoadDataFromJson<T>(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                // Обработка случая, когда файл не найден
                // Возвращаем null, чтобы показать, что группа не найдена
                return null;
            }

            // Загрузка данных из JSON файла
            string jsonData = System.IO.File.ReadAllText(filePath);

            // Десериализация JSON в список ScheduleData
            List<T> data = JsonConvert.DeserializeObject<List<T>>(jsonData);

            return data;
        }

        public List<Models.Document> LoadFilesFromDocumentsFolder(string documentsFolderPath)
        {
            string documentsPath = Path.Combine(documentsFolderPath, "documents-news-events", "documents");
            List<Models.Document> documents = new List<Models.Document>();

            string[] directories = Directory.GetDirectories(documentsPath);


            foreach (string directory in directories)
            {
                Models.Document document = new Models.Document
                {
                    DocumentName = Path.GetFileName(directory).Replace("_", " "),
                    DocumentPath = directory
                };
                documents.Add(document);
            }

            return documents;
        }

        private Dictionary<string, string> facultyCodes = new Dictionary<string, string>
        {
            {"digital_industrial_technologies", "Факультет цифровых промышленных технологий"},
            {"college_of_SMTU", "Колледж СПбГМТУ (СТФ)"},
            {"ship_power_engineering_and_automation", "Факультет корабельной энергетики и автоматики"},
            {"natural_sciences_and_humanities", "Факультет естественнонаучного и гуманитарного образования"},
            {"engineering_and_economics", "Инженерно-экономический факультет"},
            {"marine_instrument_engineering", "Факультет морского приборостроения"},
            {"shipbuilding_and_ocean_engineering", "Факультет кораблестроения и океанотехники"}
        };

        public string GetFacultyName(string facultyCode)
        {
            // Проверяем, является ли входная строка кодом факультета
            if (facultyCodes.ContainsKey(facultyCode))
            {
                return facultyCodes[facultyCode];
            }
            // Проверяем, является ли входная строка названием факультета
            else if (facultyCodes.ContainsValue(facultyCode))
            {
                return facultyCodes.FirstOrDefault(x => x.Value == facultyCode).Key;
            }
            // Если входная строка не соответствует коду или названию факультета
            else
            {
                return facultyCode;
            }
        }
    }

    public class ScheduleOptions
    {
        public string TypeWeek { get; set; }
        // Другие свойства, если они есть
    }

    #region LoadingSchedules
    public class ScheduleData
    {
        public string DayOfWeek { get; set; } // День недели
        public TimeSpan StartTime { get; set; } // Время начала
        public TimeSpan EndTime { get; set; } // Время конца
        public string WeekType { get; set; } // Тип недели (верхняя, нижняя, обе)
        public string Classroom { get; set; } // Аудитория
        public string Group { get; set; } // Группа
        public string Subject { get; set; } // Предмет
        public string InstructorName { get; set; } // ФИО преподавателя
        public string InstructorLink { get; set; } // Ссылка на преподавателя
    }

    public class CurrentSchedule
    {
        public string Group { get; set; }
        public string Subject { get; set; }
        public string Classroom { get; set; }
        public string InstructorName { get; set; }
        public string Status { get; set; }
    }
    #endregion

    #region LoadingVk

    public class VkPost
    {
        public List<string> ImageUrl { get; set; }
        public string Text { get; set; }
        public string Link { get; set; }
        public DateTime DatePost { get; set; }
    }

    #endregion
}


