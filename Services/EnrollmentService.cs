using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Services;

public interface IEnrollmentService
{
    Task<List<Enrollment>> GetAllAsync(CancellationToken ct = default);
    Task<Enrollment?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Enrollment?> EnrollAsync(int studentId, int courseId, decimal? grade = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class EnrollmentService(TmsDbContext context, ILogger<EnrollmentService> logger) : IEnrollmentService
{
    public async Task<List<Enrollment>> GetAllAsync(CancellationToken ct = default)
    {
        var enrollments = await context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .ToListAsync(ct);
        logger.LogInformation("Retrieved {Count} enrollments", enrollments.Count);
        return enrollments;
    }

    public async Task<Enrollment?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var enrollment = await context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (enrollment is null)
            logger.LogWarning("Enrollment {Id} not found", id);
        return enrollment;
    }

    public async Task<Enrollment?> EnrollAsync(int studentId, int courseId, decimal? grade = null, CancellationToken ct = default)
    {
        var existing = await context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId, ct);

        if (existing is not null)
        {
            logger.LogWarning("Duplicate enrollment — Student {StudentId} already in Course {CourseId}", studentId, courseId);
            return existing;
        }

        var enrollment = new Enrollment { StudentId = studentId, CourseId = courseId, Grade = grade };
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Enrolled Student {StudentId} in Course {CourseId} record {Id}", studentId, courseId, enrollment.Id);
        return enrollment;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var enrollment = await context.Enrollments.FindAsync([id], ct);
        if (enrollment is null)
        {
            logger.LogWarning("Delete failed — Enrollment {Id} not found", id);
            return false;
        }
        context.Enrollments.Remove(enrollment);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Deleted enrollment {Id}", id);
        return true;
    }
}
