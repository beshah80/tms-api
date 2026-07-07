namespace TmsApi.Services;

// Legacy M4 worker — retained for DI registration compatibility.
// The old IEnrollmentService interface it depended on was replaced in M6.
public class EnrollmentWorker(IServiceScopeFactory scopeFactory)
{
    public void ProcessBatch()
    {
        // No-op: batch processing moved out of scope for M6 contract sprint.
    }
}
