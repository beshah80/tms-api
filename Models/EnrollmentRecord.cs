namespace TmsApi.Models;

public record EnrollmentRecord(
    string Id,
    string StudentId,
    string CourseCode,
    DateTime EnrolledAt);

public record CreateEnrollmentRequest(string StudentId, string CourseCode);

public class TmsDatabaseException(string message) : Exception(message);
