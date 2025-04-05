using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using EduSubmit.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EduSubmit.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<Instructor> Instructors { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<Grade> Grades { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Composite Key for Submission entity
            modelBuilder.Entity<Submission>()
                .HasKey(s => new { s.StudentId, s.AssignmentId });

            // Foreign Key Relationships
            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Student)
                .WithMany(st => st.Submissions)
                .HasForeignKey(s => s.StudentId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Assignment)
                .WithMany(a => a.Submissions)
                .HasForeignKey(s => s.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);  // Avoid cascading delete

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Class)
                .WithMany(c => c.Submissions)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            // Foreign Key Relationships for Grade entity
            modelBuilder.Entity<Grade>()
                .HasKey(g => new { g.StudentId, g.AssignmentId });

            modelBuilder.Entity<Grade>()
                .HasOne(g => g.Student)
                .WithMany(st => st.Grades)
                .HasForeignKey(g => g.StudentId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Grade>()
                .HasOne(g => g.Assignment)
                .WithMany(a => a.Grades)
                .HasForeignKey(g => g.AssignmentId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Grade>()
                .HasOne(g => g.Instructor)
                .WithMany(i => i.Grades)
                .HasForeignKey(g => g.InstructorId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Grade>()
                .HasOne(g => g.Submission)
                .WithOne()
                .HasForeignKey<Grade>(g => new { g.StudentId, g.AssignmentId })
                .OnDelete(DeleteBehavior.Cascade); // or Restrict based on your requirement

            // Foreign Key Relationships for Assignment entity
            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Class)
                .WithMany(c => c.Assignments)
                .HasForeignKey(a => a.ClassId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            // Foreign Key Relationships for Instructor entity
            modelBuilder.Entity<Instructor>()
                .HasOne(i => i.Organization)
                .WithMany(o => o.Instructors)
                .HasForeignKey(i => i.OrganizationId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Instructor>()
                .HasIndex(u => u.EmailAddress)
                .IsUnique();

            // Foreign Key Relationships for Organization entity
            modelBuilder.Entity<Organization>()
                .HasMany(o => o.Instructors)
                .WithOne(i => i.Organization)
                .HasForeignKey(i => i.OrganizationId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Organization>()
                .HasMany(o => o.Students)
                .WithOne(s => s.Organization)
                .HasForeignKey(s => s.OrganizationId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Organization>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Organization>()
                .HasIndex(u => u.EmailAddress)
                .IsUnique();

            // Foreign Key Relationships for Student entity
            modelBuilder.Entity<Student>()
                .HasOne(s => s.Organization)
                .WithMany(o => o.Students)
                .HasForeignKey(s => s.OrganizationId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Student>()
                .HasOne(s => s.Class)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Student>()
                .HasIndex(u => u.EmailAddress)
                .IsUnique();

            // Foreign Key Relationships for Class entity
            modelBuilder.Entity<Class>()
                .HasMany(c => c.Assignments)
                .WithOne(a => a.Class)
                .HasForeignKey(a => a.ClassId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Class>()
                .HasMany(c => c.Students)
                .WithOne(s => s.Class)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Class>()
                .HasMany(c => c.Submissions)
                .WithOne(s => s.Class)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.NoAction);  // Avoid cascading delete

            modelBuilder.Entity<Class>()
                .Property(c => c.ClassName)
                .HasColumnType("varchar(50)"); // Ensure PostgreSQL-compatible type

            var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
            );

            var dateTimeNullableConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v
            );

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(dateTimeNullableConverter);
                    }
                }
            }
        }
    }
}