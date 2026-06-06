using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
builder.Services.AddAuthorization();

var app = builder.Build();

// EXERCISE 1B: Middleware in the specified order
app.UseMiddleware<RequestLoggingMiddleware>(); // FIRST - outer wrapper
app.UseExceptionHandler("/Error");            // Exception handling
app.UseHttpsRedirection();                    // HTTPS redirect
app.UseRouting();                            // Routing
app.UseAuthentication();                     // Authentication
app.UseAuthorization();                      // Authorization

// Map the protected endpoint
app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001", 
    letterGrade = "A"
})).RequireAuthorization();

app.MapControllers();
app.Run();