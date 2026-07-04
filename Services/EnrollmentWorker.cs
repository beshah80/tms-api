using TmsApi.Services;
using TmsApi.Entities;

namespace TmsApi.Services;

public class EnrollmentWorker
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EnrollmentWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void ProcessBatch()
    {
        using var scope = _scopeFactory.CreateScope();
        var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();

        var enrollments = enrollmentService.GetAllAsync().Result;
        Console.WriteLine($"Processing {enrollments.Count} enrollments for scholarship recalculation");

        foreach (Enrollment enrollment in enrollments)
            Console.WriteLine($"Recalculating scholarship for student {enrollment.StudentId} in course {enrollment.CourseId}");
    }
}
