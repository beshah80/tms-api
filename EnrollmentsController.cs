using Microsoft.AspNetCore.Mvc;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/enrollments")]
public class EnrollmentsController : ControllerBase
{
    private readonly IEnrollmentService _enrollmentService;

    // Constructor – the waiter gets the kitchen (service) injected
    public EnrollmentsController(IEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    // GET /api/enrollments → returns all enrollments
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var enrollments = await _enrollmentService.GetAllAsync();
        return Ok(enrollments);  // 200 OK with list
    }

    // GET /api/enrollments/{id} → returns one enrollment or 404
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var record = await _enrollmentService.GetByIdAsync(id);
        if (record is null)
            return NotFound();   // 404
        return Ok(record);       // 200 with the record
    }

    // POST /api/enrollments → creates a new enrollment
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEnrollmentRequest request)
    {
        var record = await _enrollmentService.EnrollAsync(request.StudentId, request.CourseCode);
        // 201 Created + Location header pointing to GetById action
        return CreatedAtAction(nameof(GetById), new { id = record.Id }, record);
    }

    // DELETE /api/enrollments/{id} → deletes enrollment
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _enrollmentService.DeleteAsync(id);
        if (!deleted)
            return NotFound();   // 404 if not found
        return NoContent();      // 204 No Content on success
    }
}

// Request DTO (what the client sends when creating)
public record CreateEnrollmentRequest(string StudentId, string CourseCode);