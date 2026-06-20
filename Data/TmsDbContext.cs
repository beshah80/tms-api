using Microsoft.EntityFrameworkCore;
using TmsApi.Entities;

namespace TmsApi.Data;

public class TmsDbContext(DbContextOptions<TmsDbContext> options) : DbContext(options)
{
    public DbSet<TmsApi.Entities.Student> Students => Set<TmsApi.Entities.Student>();
    public DbSet<TmsApi.Entities.Course> Courses => Set<TmsApi.Entities.Course>();
    public DbSet<TmsApi.Entities.Enrollment> Enrollments => Set<TmsApi.Entities.Enrollment>();
    public DbSet<TmsApi.Entities.Assessment> Assessments => Set<TmsApi.Entities.Assessment>();
    public DbSet<TmsApi.Entities.Certificate> Certificates => Set<TmsApi.Entities.Certificate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TmsDbContext).Assembly);
    }
}