using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(TmsDbContext context) : ControllerBase
{
    private const int PageSize = 2;
    private const int TopCourses = 5;

    // TODO 1: Pagination — OrderBy, Skip, Take, ToListAsync with CancellationToken
    [HttpGet("students")]
    public async Task<IActionResult> GetStudentsPaged([FromQuery] int page = 1, [FromQuery] int pageSize = PageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = PageSize;

        var students = await context.Students
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { Page = page, PageSize = pageSize, Data = students });
    }

    // TODO 2: Top N courses by enrollment — GroupBy, order by count, Take(top)
    [HttpGet("top-courses")]
    public async Task<IActionResult> GetTopCourses([FromQuery] int top = TopCourses, CancellationToken ct = default)
    {
        if (top < 1) top = TopCourses;

        var topCourses = await context.Enrollments
            .GroupBy(e => e.Course.Title)
            .Select(g => new
            {
                CourseTitle = g.Key,
                EnrollmentCount = g.Count()
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .Take(top)
            .ToListAsync(ct);

        return Ok(topCourses);
    }
}
