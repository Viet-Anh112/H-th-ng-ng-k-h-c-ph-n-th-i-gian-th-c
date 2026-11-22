using Microsoft.EntityFrameworkCore;

namespace RegistrationApi.Data;

public class RegistrationDbContext : DbContext
{
    public RegistrationDbContext(DbContextOptions<RegistrationDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Student>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(20);
            e.Property(x => x.FullName).HasMaxLength(100);
        });

        b.Entity<Registration>(e =>
        {
            e.Property(x => x.StudentId).HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId);

            // Chống đăng ký trùng ở tầng CSDL: một sinh viên chỉ có 1 đăng ký "Active" cho mỗi lớp.
            e.HasIndex(x => new { x.StudentId, x.CourseId })
             .IsUnique()
             .HasFilter("\"Status\" = 'Active'");
        });

        b.Entity<Student>().HasData(
            new Student { Id = "SV001", FullName = "Nguyễn Văn An" },
            new Student { Id = "SV002", FullName = "Trần Thị Bình" },
            new Student { Id = "SV003", FullName = "Lê Văn Cường" },
            new Student { Id = "SV004", FullName = "Phạm Thị Dung" }
        );
    }
}
