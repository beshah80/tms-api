using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();


builder.Services.AddSingleton<EnrollmentWorker>();         
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>(); 


builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart(); 


builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

var app = builder.Build();


app.UseMiddleware<RequestLoggingMiddleware>(); // FIRST - outer wrapper
app.UseExceptionHandler("/Error");  
app.UseStatusCodePages();          // Exception handling
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

// EXERCISE 2: Test route to trigger the captive dependency
app.MapGet("/api/error", () =>
{
    throw new Exception("Simulated database failure for ProblemDetails testing");
});

// EXERCISE 4: Test endpoints for logging
app.MapPost("/api/enrollments/test", async (IEnrollmentService service) =>
{
    Console.WriteLine("=== TESTING DUPLICATE ENROLLMENT ===");
    
    // First enrollment - should succeed
    Console.WriteLine("1. First enrollment attempt...");
    var enrollment1 = await service.EnrollAsync("S-001", "CS-101");
    
    // Second enrollment - should show duplicate warning
    Console.WriteLine("2. Second enrollment attempt (same student, same course)...");
    var enrollment2 = await service.EnrollAsync("S-001", "CS-101");
    
    Console.WriteLine("=== TESTING MISSING RECORDS ===");
    
    // Test existing record
    Console.WriteLine("3. Looking for existing record...");
    var found = await service.GetByIdAsync(enrollment1.Id);
    
    // Test missing record  
    Console.WriteLine("4. Looking for non-existent record...");
    var notFound = await service.GetByIdAsync("nonexistent");
    
    Console.WriteLine("=== TESTING DELETE ===");
    
    // First delete - should succeed
    Console.WriteLine("5. First delete attempt...");
    var deleted = await service.DeleteAsync(enrollment1.Id);
    
    // Second delete - should show not found warning
    Console.WriteLine("6. Second delete attempt (already deleted)...");
    var deletedAgain = await service.DeleteAsync(enrollment1.Id);
    
    return Results.Ok("Logging test completed - check console for structured logs");
});

app.MapControllers();
app.Run();