using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/performance-test")]
public class PerformanceTestController(TmsDbContext context) : ControllerBase
{
    // Part A: Intentional N+1 — produces 1 + N SQL statements
    [HttpGet("n-plus-one")]
    public async Task<IActionResult> NPlusOne(CancellationToken ct)
    {
        Console.WriteLine("\n>>> PART A: N+1 — watch how many SQL statements appear below...");

        var students = await context.Students.AsNoTracking().ToListAsync(ct);  // 1 query

        var result = new List<object>();
        foreach (var s in students)
        {
            // 1 extra query PER student — this is the N+1 trap
            var count = await context.Enrollments
                .AsNoTracking()
                .CountAsync(e => e.StudentId == s.Id, ct);

            Console.WriteLine($"{s.Name}: {count} enrollments");
            result.Add(new { s.Name, EnrollmentCount = count });
        }

        Console.WriteLine(">>> PART A done — count the SQL statements above\n");
        return Ok(result);
    }

    // Part B: Fix with projection — produces 1 SQL statement
    [HttpGet("shaped")]
    public async Task<IActionResult> Shaped(CancellationToken ct)
    {
        Console.WriteLine("\n>>> PART B: Shaped query — should be 1 SQL statement...");

        var report = await context.Students
            .AsNoTracking()
            .Select(s => new
            {
                s.Name,
                EnrollmentCount = s.Enrollments.Count
            })
            .ToListAsync(ct);

        foreach (var r in report)
            Console.WriteLine($"{r.Name}: {r.EnrollmentCount} enrollments");

        Console.WriteLine(">>> PART B done — only 1 SQL statement above\n");
        return Ok(report);
    }

    // Part B alternative: Fix with Include — loads full enrollment objects
    [HttpGet("include")]
    public async Task<IActionResult> WithInclude(CancellationToken ct)
    {
        Console.WriteLine("\n>>> PART B (Include): Single query with JOIN...");

        var students = await context.Students
            .AsNoTracking()
            .Include(s => s.Enrollments)
            .ToListAsync(ct);

        var result = students.Select(s => new { s.Name, EnrollmentCount = s.Enrollments.Count });

        foreach (var r in result)
            Console.WriteLine($"{r.Name}: {r.EnrollmentCount} enrollments");

        Console.WriteLine(">>> PART B (Include) done\n");
        return Ok(result);
    }
}
