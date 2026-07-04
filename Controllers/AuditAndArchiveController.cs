using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/audit-archive")]
public class AuditAndArchiveController(TmsDbContext context) : ControllerBase
{
    // Exercise 8: Update student and stamp LastUpdated shadow property
    [HttpPut("student/{id}/update-name")]
    public async Task<IActionResult> UpdateStudentName(int id, [FromBody] string newName, CancellationToken ct)
    {
        var student = await context.Students.FindAsync([id], ct);
        if (student is null) return NotFound(new { Message = $"Student {id} not found" });

        student.Name = newName;

        // Set the shadow property — invisible on the entity, stored in the database
        context.Entry(student).Property("LastUpdated").CurrentValue = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        var lastUpdated = context.Entry(student).Property("LastUpdated").CurrentValue;
        return Ok(new { student.Id, student.Name, LastUpdated = lastUpdated });
    }

    // Exercise 8: Concurrency test — load student with Version token
    [HttpGet("student/{id}/with-version")]
    public async Task<IActionResult> GetWithVersion(int id, CancellationToken ct)
    {
        var student = await context.Students.FindAsync([id], ct);
        if (student is null) return NotFound();
        return Ok(new { student.Id, student.Name, student.GPA, student.Version });
    }

    // Exercise 9: Soft delete a student — sets IsDeleted = true
    [HttpDelete("student/{id}/soft")]
    public async Task<IActionResult> SoftDelete(int id, CancellationToken ct)
    {
        var student = await context.Students.FindAsync([id], ct);
        if (student is null) return NotFound(new { Message = $"Student {id} not found" });

        student.IsDeleted = true;
        context.Entry(student).Property("LastUpdated").CurrentValue = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new { Message = $"Student '{student.Name}' soft deleted — hidden from normal queries" });
    }

    // Exercise 9: Normal query — soft-deleted students are invisible (HasQueryFilter)
    [HttpGet("students/active")]
    public async Task<IActionResult> GetActiveStudents(CancellationToken ct)
    {
        Console.WriteLine("\n>>> Normal query — HasQueryFilter hides IsDeleted students...");
        var students = await context.Students.AsNoTracking().ToListAsync(ct);
        Console.WriteLine($">>> Returned {students.Count} students (deleted ones hidden)\n");
        return Ok(students);
    }

    // Exercise 9: Admin query — bypasses HasQueryFilter to see all students
    [HttpGet("students/all")]
    public async Task<IActionResult> GetAllStudentsAdmin(CancellationToken ct)
    {
        Console.WriteLine("\n>>> Admin query — IgnoreQueryFilters() bypasses soft-delete filter...");
        var students = await context.Students.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        Console.WriteLine($">>> Returned {students.Count} students (including deleted)\n");
        return Ok(students);
    }

    // Exercise 9: Bulk archive enrollments older than a cutoff — single UPDATE statement
    [HttpPost("enrollments/archive")]
    public async Task<IActionResult> BulkArchive([FromQuery] int daysOld = 30, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysOld);

        Console.WriteLine($"\n>>> Bulk archive — single UPDATE for enrollments before {cutoff:yyyy-MM-dd}...");

        var affected = await context.Enrollments
            .Where(e => e.EnrolledAt < cutoff && !e.IsArchived)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, true), ct);

        Console.WriteLine($">>> Archived {affected} enrollments in one SQL UPDATE\n");
        return Ok(new { ArchivedCount = affected, Cutoff = cutoff });
    }

    // Exercise 9: View archived enrollments
    [HttpGet("enrollments/archived")]
    public async Task<IActionResult> GetArchivedEnrollments(CancellationToken ct)
    {
        var archived = await context.Enrollments
            .Where(e => e.IsArchived)
            .Select(e => new { e.Id, e.StudentId, e.CourseId, e.EnrolledAt })
            .ToListAsync(ct);
        return Ok(new { Count = archived.Count, Enrollments = archived });
    }
}
