using Microsoft.EntityFrameworkCore;

namespace CapacityService.Data;

public class CapacityDbContext : DbContext
{
    public CapacityDbContext(DbContextOptions<CapacityDbContext> options) : base(options) { }

    public DbSet<Course> Courses => Set<Course>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Course>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.ToTable(t => t.HasCheckConstraint(
                "ck_course_registered",
                "\"Registered\" >= 0 AND \"Registered\" <= \"Capacity\""));
        });

        b.Entity<Course>().HasData(
            new Course { Id = 1, Code = "INT3306-01", Name = "Phát triển ứng dụng Web", Capacity = 3,  Registered = 0 },
            new Course { Id = 2, Code = "INT3307-01", Name = "An toàn mạng",            Capacity = 40, Registered = 0 },
            new Course { Id = 3, Code = "INT3401-01", Name = "Trí tuệ nhân tạo",        Capacity = 50, Registered = 0 }
        );
    }
}
