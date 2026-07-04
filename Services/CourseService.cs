using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Services;

public interface ICourseService
{
    Task<List<TmsApi.Entities.Course>> GetAllAsync(CancellationToken ct = default);
    Task<TmsApi.Entities.Course?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TmsApi.Entities.Course> CreateAsync(TmsApi.Entities.Course course, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class CourseService(TmsDbContext context, ILogger<CourseService> logger) : ICourseService
{
    public async Task<List<TmsApi.Entities.Course>> GetAllAsync(CancellationToken ct = default)
    {
        var courses = await context.Courses.ToListAsync(ct);
        logger.LogInformation("Retrieved {Count} courses", courses.Count);
        return courses;
    }

    public async Task<TmsApi.Entities.Course?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var course = await context.Courses.FindAsync([id], ct);
        if (course is null)
            logger.LogWarning("Course {Id} not found", id);
        return course;
    }

    public async Task<TmsApi.Entities.Course> CreateAsync(TmsApi.Entities.Course course, CancellationToken ct = default)
    {
        context.Courses.Add(course);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Created course {Title} with Id {Id}", course.Title, course.Id);
        return course;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var course = await context.Courses.FindAsync([id], ct);
        if (course is null)
        {
            logger.LogWarning("Delete failed — Course {Id} not found", id);
            return false;
        }
        context.Courses.Remove(course);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Deleted course {Title} Id {Id}", course.Title, id);
        return true;
    }
}
