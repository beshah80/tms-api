using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using Scalar.AspNetCore;
using TmsApi.Middleware;
using TmsApi.Models;
using TmsApi.Services;


using TmsApi.Filters;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});
builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
        .LogTo(Console.WriteLine, LogLevel.Information)
        .EnableSensitiveDataLogging());

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();


builder.Services.AddSingleton<EnrollmentWorker>();         
builder.Services.AddScoped<IEnrollmentService, TmsApi.Services.EnrollmentService>();
builder.Services.AddScoped<IStudentService, TmsApi.Services.StudentService>();
builder.Services.AddScoped<ICourseService, TmsApi.Services.CourseService>();

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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<RequestLoggingMiddleware>();  // must come after exception handler
app.UseStatusCodePages();
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

app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seederContext = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    await DataSeeder.SeedAsync(seederContext);
}

app.Run();