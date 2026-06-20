using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/test")]
public class TestController(TmsDbContext context) : ControllerBase
{
    [HttpGet("deferred")]
    public IActionResult TestDeferred()
    {
        Console.WriteLine("\n>>> STEP 1: Building the query object (nodatabase contact)...");
        var query = context.Students.Where(s => s.GPA >= 3.0m);
        
        Console.WriteLine(">>> STEP 2: Appending a sorting clause...");
        var orderedQuery = query.OrderBy(s => s.Name);
        
        Console.WriteLine(">>> STEP 3: Materializing query into a C#List...");
        var results = orderedQuery.ToList(); // Execution is triggeredhere
        
        Console.WriteLine(">>> STEP 4: Materialization finished. Listpopulated.\n");
        return Ok(results);
    }

    // Non-translatable helper method
    private static bool IsHonorRoll(decimal gpa)
    {
        return gpa >= 3.5m;
    }

    [HttpGet("translation-fail")]
    public IActionResult TestTranslationFail()
    {
        Console.WriteLine("\n>>> STEP 1: Running non-translatable query...");
        try
        {
            var students = context.Students
                .Where(s => IsHonorRoll(s.GPA)) // EF Core does not know how to map this method to SQL
                .ToList();
            return Ok(students);
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> EXCEPTION CAUGHT: {ex.Message}\n");
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("top-enrolled-courses")]
    public async Task<IActionResult> TopEnrolledCourses()
    {
        Console.WriteLine("\n>>> Querying top enrolled courses...");
        var list = await context.Courses
            .Select(c => new
            {
                c.Title,
                EnrollmentCount = c.Enrollments.Count
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .ToListAsync();
        Console.WriteLine(">>> Done.\n");
        return Ok(list);
    }

    [HttpGet("average-gpa-per-course")]
    public async Task<IActionResult> AverageGpaPerCourse()
    {
        Console.WriteLine("\n>>> Querying average GPA per course...");
        var list = await context.Enrollments
            .GroupBy(e => e.Course.Title)
            .Select(g => new
            {
                Course = g.Key,
                AverageGPA = g.Average(e => e.Student.GPA)
            })
            .ToListAsync();
        Console.WriteLine(">>> Done.\n");
        return Ok(list);
    }

    [HttpGet("students-no-enrollments")]
    public async Task<IActionResult> StudentsWithNoEnrollments()
    {
        Console.WriteLine("\n>>> Approach A: Using subquery (NOT EXISTS)...");
        var listA = await context.Students
            .Where(s => !s.Enrollments.Any())
            .Select(s => s.Name)
            .ToListAsync();
        Console.WriteLine(">>> Approach A done.");

        Console.WriteLine(">>> Approach B: Using LeftJoin (LEFT JOIN ... WHERE NULL)...");
        var listB = await context.Students
            .LeftJoin(context.Enrollments,
                s => s.Id,
                e => e.StudentId,
                (s, e) => new { s, e })
            .Where(x => x.e == null)
            .Select(x => x.s.Name)
            .ToListAsync();
        Console.WriteLine(">>> Approach B done.\n");

        return Ok(new { ApproachA = listA, ApproachB = listB });
    }

    [HttpPost("add-student")]
    public IActionResult AddStudent([FromBody] TmsApi.Entities.Student student)
    {
        Console.WriteLine("\n>>> POST: Adding new student to database...");
        context.Students.Add(student);
        context.SaveChanges();
        Console.WriteLine($">>> POST: Student '{student.Name}' saved with Id={student.Id}\n");
        return CreatedAtAction(nameof(TestDeferred), new { }, student);
    }
}



//change them with context
