using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Services;

public interface IStudentService
{
    Task<List<TmsApi.Entities.Student>> GetAllAsync(CancellationToken ct = default);
    Task<TmsApi.Entities.Student?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TmsApi.Entities.Student> CreateAsync(TmsApi.Entities.Student student, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class StudentService(TmsDbContext context, ILogger<StudentService> logger) : IStudentService
{
    public async Task<List<TmsApi.Entities.Student>> GetAllAsync(CancellationToken ct = default)
    {
        var students = await context.Students.ToListAsync(ct);
        logger.LogInformation("Retrieved {Count} students", students.Count);
        return students;
    }

    public async Task<TmsApi.Entities.Student?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var student = await context.Students.FindAsync([id], ct);
        if (student is null)
            logger.LogWarning("Student {Id} not found", id);
        return student;
    }

    public async Task<TmsApi.Entities.Student> CreateAsync(TmsApi.Entities.Student student, CancellationToken ct = default)
    {
        context.Students.Add(student);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Created student {Name} with Id {Id}", student.Name, student.Id);
        return student;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var student = await context.Students.FindAsync([id], ct);
        if (student is null)
        {
            logger.LogWarning("Delete failed — Student {Id} not found", id);
            return false;
        }
        context.Students.Remove(student);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Deleted student {Name} Id {Id}", student.Name, id);
        return true;
    }
}
