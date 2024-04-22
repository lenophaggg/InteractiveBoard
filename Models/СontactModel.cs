namespace MyMvcApp.Models
{
    public class MainUniversityContact
    {
        public string NameContact { get; set; }
        public string AdditionalName { get; set; }
        public string Telephone { get; set; }
        public string WorkTime { get; set; }
        public string Address { get; set; }
        public string Information { get; set; }
    }

    public class PersonContact
    {
        public string IdContact { get; set; }
        public string NameContact { get; set; }
        public List<string> Position { get; set; }
        public List<string> TaughtSubjects { get; set; }
        public string AcademicDegree { get; set; }
        public string TeachingExperience { get; set; }
        public string Telephone { get; set; }
        public string Email { get; set; }
        public string ScheduleLink { get; set; }
        public string Information { get; set; }
        public string ImgPath { get; set; }
    }
}
