using Asp.Versioning;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Data;
using TmsApi.ExceptionHandlers;
using TmsApi.Filters;
using TmsApi.Middleware;
using TmsApi.Models;
using TmsApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ── MediatR + FluentValidation ────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly));

builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);

// LoggingBehavior FIRST — it must wrap ValidationBehavior
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// ── Exception handler (before AddProblemDetails) ──────────────────────────────
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── API versioning ────────────────────────────────────────────────────────────
builder.Services.AddOpenApi("v1", options =>
    options.ShouldInclude = d => d.GroupName == "v1");

builder.Services.AddOpenApi("v2", options =>
    options.ShouldInclude = d => d.GroupName == "v2");

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion                   = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions                   = true;
    options.ApiVersionReader                    = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat           = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ── Controllers + global audit filter ────────────────────────────────────────
builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
        .LogTo(Console.WriteLine, LogLevel.Information)
        .EnableSensitiveDataLogging());

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IEnrollmentService, TmsApi.Services.EnrollmentService>();
builder.Services.AddScoped<IStudentService,    TmsApi.Services.StudentService>();
builder.Services.AddScoped<ICourseService,     TmsApi.Services.CourseService>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes  = true;
    options.ValidateOnBuild = true;
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// UseExceptionHandler FIRST — before any middleware that can throw
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/{documentName}/openapi.json");
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TMS API Reference")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
               .AddDocument("v1", "API Version 1.0")
               .AddDocument("v2", "API Version 2.0");
    });
}

// V1 deprecation headers — before MapControllers so every V1 response is stamped
app.UseMiddleware<V1DeprecationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode  = "CS-101",
    studentId   = "S-001",
    letterGrade = "A"
})).RequireAuthorization();

app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    using var scope        = app.Services.CreateScope();
    var seederContext      = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    await DataSeeder.SeedAsync(seederContext);
}

app.Run();
