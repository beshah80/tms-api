using Microsoft.EntityFrameworkCore;
using TmsApi.Entities;

namespace TmsApi.Data;

public class TmsDbContext(DbContextOptions<TmsDbContext> options) : DbContext(options)
{
    public DbSet<TmsApi.Entities.Student> Students => Set<TmsApi.Entities.Student>();
    public DbSet<TmsApi.Entities.Course> Courses => Set<TmsApi.Entities.Course>();
    public DbSet<TmsApi.Entities.Enrollment> Enrollments => Set<TmsApi.Entities.Enrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TmsApi.Entities.Student>().HasKey(s => s.Id);
        modelBuilder.Entity<TmsApi.Entities.Course>().HasKey(c => c.Id);
        modelBuilder.Entity<TmsApi.Entities.Enrollment>().HasKey(e => e.Id);
    }
}