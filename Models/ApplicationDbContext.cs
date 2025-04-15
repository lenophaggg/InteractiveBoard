using Microsoft.EntityFrameworkCore;

namespace MyMvcApp.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public async Task ClearOldSchedulesFacultiesClassroomsGroupsAsync()
        {
            // Очистка таблицы
            await Database.ExecuteSqlRawAsync("DELETE FROM public.schedules");

            await Database.ExecuteSqlRawAsync("ALTER SEQUENCE public.schedules_scheduleid_seq RESTART WITH 1");

            await Database.ExecuteSqlRawAsync("DELETE FROM public.actual_groups");

            await Database.ExecuteSqlRawAsync("DELETE FROM public.classrooms");

        }

        public DbSet<ScheduleData> ScheduleData { get; set; }

        public DbSet<MainUniversityContact> MainUniversityContacts { get; set; }
        public DbSet<PersonContact> PersonContacts { get; set; }

        public DbSet<PersonTaughtSubject> PersonTaughtSubjects { get; set; }
        public DbSet<Subject> Subjects { get; set; }

        public DbSet<Faculties> Faculties { get; set; }
        public DbSet<Groups> Groups { get; set; }
        public DbSet<ActualGroup> ActualGroups { get; set; }


        public DbSet<Classrooms> Classrooms { get; set; }
    }
}
