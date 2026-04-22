using Microsoft.EntityFrameworkCore;
using NAAC.Models;

namespace NAAC.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserOTP> UserOTPs { get; set; }
        public DbSet<Criteria> Criteria { get; set; }
        public DbSet<CriteriaAssignment> CriteriaAssignments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<NAACTable> NAACTables { get; set; }
        public DbSet<TableColumn> TableColumns { get; set; }
        public DbSet<DataRecord> DataRecords { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed System settings
            modelBuilder.Entity<SystemSetting>().HasData(new SystemSetting
            {
                Id = 1,
                SystemName = "NAAC Portal",
                InstitutionName = "National Assessment and Accreditation Council",
                SystemLogo = "/images/naac-logo.png",
                UpdatedAt = DateTime.Now
            });
            
            // Uniqueness constraint for Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Seed Criteria List
            modelBuilder.Entity<Criteria>().HasData(
                new Criteria { Id = 1, Number = "5.1.1", Title = "Scholarship/Free-ship" },
                new Criteria { Id = 2, Number = "5.1.2", Title = "Skill Development" },
                new Criteria { Id = 3, Number = "5.1.3", Title = "Career Guidance" },
                new Criteria { Id = 4, Number = "5.2.1", Title = "Placement of Students" },
                new Criteria { Id = 5, Number = "5.2.2", Title = "Student Progression" },
                new Criteria { Id = 6, Number = "5.2.3", Title = "Graduating Students Exam" },
                new Criteria { Id = 7, Number = "5.3.1", Title = "Awards/Medals" },
                new Criteria { Id = 8, Number = "5.3.3", Title = "Sports/Cultural Events" },
                new Criteria { Id = 9, Number = "5.4.1", Title = "Alumni Engagement" },
                new Criteria { Id = 10, Number = "6.3.3", Title = "FDP/Professional Training" },
                new Criteria { Id = 11, Number = "6.3.4", Title = "Faculty Attendance FDP" },
                new Criteria { Id = 12, Number = "6.5.2", Title = "Quality Assurance" },
                new Criteria { Id = 13, Number = "3.4.7", Title = "Research Publications" }
            );

            // Seed Admin User
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = 1,
                FullName = "System Administrator",
                Email = "admin@gmail.com",
                MobileNumber = "0000000000",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
                CollegeName = "NAAC Central",
                UniversityName = "National University",
                Role = "Admin",
                IsEmailVerified = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
    }
}
