namespace MyMvcApp.Models
{
    public class PersonContactDto
    {
        public int IdContact { get; set; }
        public string UniversityIdContact { get; set; }
        public string NameContact { get; set; }
        public string[] Position { get; set; }
        public string AcademicDegree { get; set; }
        public string TeachingExperience { get; set; }
        public string Telephone { get; set; }
        public string Email { get; set; }
        public string Information { get; set; }
        public string ImgPath { get; set; }
        public List<string> TaughtSubjects { get; set; } // List of subject names
        public List<string> TaughtGroups { get; set; }   // List of group numbers
    }
}
