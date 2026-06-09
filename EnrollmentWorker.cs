// FIXED VERSION - Uses IServiceScopeFactory to avoid captive dependency
public class EnrollmentWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    // SOLUTION: Inject the scope factory, not the scoped service
    public EnrollmentWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
    
    public void ProcessBatch()
    {
        // SOLUTION: Create a short-lived scope for this operation
        using var scope = _scopeFactory.CreateScope();
        
        // SOLUTION: Get the scoped service from the temporary scope
        var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
        
        // Now use the service safely
        var enrollments = enrollmentService.GetAllAsync().Result;
        Console.WriteLine($"Processing {enrollments.Count} enrollments for scholarship recalculation");
        
        foreach (var enrollment in enrollments)
        {
            Console.WriteLine($"Recalculating scholarship for student {enrollment.StudentId} in course {enrollment.CourseCode}");
        }
        
        // The 'using' automatically disposes the scope and its services
    }
}