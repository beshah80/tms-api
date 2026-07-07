using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Dtos;
using TmsApi.Entities;
using TmsStudent = TmsApi.Entities.Student;
using TmsCourse = TmsApi.Entities.Course;
using TmsEnrollment = TmsApi.Entities.Enrollment;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/relationship-test")]
public class RelationshipTestController(TmsDbContext context) : ControllerBase
{
    // Create a student
    [HttpPost("student")]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequest req)
    {
        var student = new TmsStudent
        {
            RegistrationNumber = req.RegistrationNumber,
            Name = req.Name,
            GPA = req.GPA
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();
        return Ok(new { student.Id, student.Name, student.RegistrationNumber, student.GPA });
    }

    // Create a course
    [HttpPost("course")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest req)
    {
        var course = new TmsCourse
        {
            Code = req.Code,
            Title = req.Title,
            MaxCapacity = req.MaxCapacity
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();
        return Ok(new { course.Id, course.Code, course.Title, course.MaxCapacity });
    }

    // Create an enrollment — links a student to a course
    [HttpPost("enrollment")]
    public async Task<IActionResult> CreateEnrollment([FromBody] CreateEnrollmentRequest req)
    {
        var student = await context.Students.FindAsync(req.StudentId);
        if (student is null) return NotFound(new { Message = $"Student {req.StudentId} not found" });

        var course = await context.Courses.FindAsync(req.CourseId);
        if (course is null) return NotFound(new { Message = $"Course {req.CourseId} not found" });

        var enrollment = new TmsEnrollment
        {
            StudentId = req.StudentId,
            CourseId = req.CourseId,
            Grade = req.Grade
        };
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();
        return Ok(new
        {
            enrollment.Id,
            Student = student.Name,
            Course = course.Title,
            enrollment.Grade,
            enrollment.EnrolledAt
        });
    }

    // Try to delete a Student that has enrollments — should be blocked by Restrict
    [HttpDelete("student/{id}")]
    public async Task<IActionResult> DeleteStudent(int id)
    {
        var student = await context.Students.FindAsync(id);
        if (student is null) return NotFound(new { Message = $"Student {id} not found" });

        try
        {
            context.Students.Remove(student);
            await context.SaveChangesAsync();
            return Ok(new { Message = $"Student '{student.Name}' deleted successfully" });
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new
            {
                Message = $"Cannot delete Student '{student.Name}' — they have enrollments. Delete enrollments first.",
                Reason = ex.InnerException?.Message
            });
        }
    }

    // Try to delete a Course that has enrollments — should be blocked by Restrict
    [HttpDelete("course/{id}")]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        var course = await context.Courses.FindAsync(id);
        if (course is null) return NotFound(new { Message = $"Course {id} not found" });

        try
        {
            context.Courses.Remove(course);
            await context.SaveChangesAsync();
            return Ok(new { Message = $"Course '{course.Title}' deleted successfully" });
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new
            {
                Message = $"Cannot delete Course '{course.Title}' — it has enrollments. Delete enrollments first.",
                Reason = ex.InnerException?.Message
            });
        }
    }

    // Delete enrollment first, then the student — should succeed
    [HttpDelete("student/{id}/force")]
    public async Task<IActionResult> ForceDeleteStudent(int id)
    {
        var student = await context.Students
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student is null) return NotFound(new { Message = $"Student {id} not found" });

        var enrollmentCount = student.Enrollments.Count;
        context.Enrollments.RemoveRange(student.Enrollments);
        context.Students.Remove(student);
        await context.SaveChangesAsync();

        return Ok(new
        {
            Message = $"Student '{student.Name}' deleted successfully after removing {enrollmentCount} enrollments"
        });
    }
}

public record CreateStudentRequest(string RegistrationNumber, string Name, decimal GPA);
public record CreateEnrollmentRequest(int StudentId, int CourseId, decimal? Grade);
