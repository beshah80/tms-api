using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/enrollments")]
public class EnrollmentsController(TmsDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var enrollments = await context.Enrollments.ToListAsync();
        return Ok(enrollments);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var enrollment = await context.Enrollments.FindAsync(id);
        if (enrollment is null) return NotFound();
        return Ok(enrollment);
    }

    [HttpPost]
    public async Task<IActionResult> Create(TmsApi.Entities.Enrollment enrollment)
    {
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = enrollment.Id }, enrollment);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, TmsApi.Entities.Enrollment enrollment)
    {
        if (id != enrollment.Id) return BadRequest();
        
        context.Entry(enrollment).State = EntityState.Modified;
        
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!context.Enrollments.Any(e => e.Id == id)) return NotFound();
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var enrollment = await context.Enrollments.FindAsync(id);
        if (enrollment is null) return NotFound();

        context.Enrollments.Remove(enrollment);
        await context.SaveChangesAsync();
        return NoContent();
    }
}
