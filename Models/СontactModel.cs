using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models
{
    [PrimaryKey("NameContact", "AdditionalName")]
    [Table("university_main_contacts")]
    public class MainUniversityContact
    {        
        [Column("namecontact")]
        public string NameContact { get; set; }
        
        [Column("additionalname")]
        public string AdditionalName { get; set; }
        [Column("telephone")]
        public string? Telephone { get; set; }
        [Column("worktime")]
        public string? WorkTime { get; set; }
        [Column("address")]
        public string? Address { get; set; }
        [Column("information")]
        public string? Information { get; set; }
    }

    [PrimaryKey("IdContact")]
    [Table("person_contacts")]
    public class PersonContact
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("idcontact")]
        public int IdContact { get; set; }

        [Column("university_idcontact")]
        public string UniversityIdContact { get; set; }

        [Column("namecontact")]
        public string NameContact { get; set; }

        [Column("position")]
        public string[]? Position { get; set; }

        [Column("academicdegree")]
        public string? AcademicDegree { get; set; }

        [Column("teachingexperience")]
        public string? TeachingExperience { get; set; }

        [Column("telephone")]
        public string? Telephone { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("information")]
        public string? Information { get; set; }

        [Column("imgpath")]
        public string? ImgPath { get; set; }

        // Навигационное свойство для связанных предметов, преподаваемых контактом
        public ICollection<PersonTaughtSubject> TaughtSubjects { get; set; }
    }
}
