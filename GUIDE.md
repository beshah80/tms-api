# Module 4 ASP.NET Core 10 Fundamentals - Complete Lab Guide

## Overview

This guide covers **Module 4 Lab Sessions 1 & 2** - building a production-ready Training Management System (TMS) API with proper request pipeline, dependency injection, configuration, and logging.

## Prerequisites

- .NET 10 SDK installed
- VS Code or Visual Studio
- Basic understanding of C# and HTTP

---

## SESSION 1: Request Flow & Visibility

### Exercise 1: The Blind Server (Middleware Ordering)

**Goal**: Fix middleware ordering to prevent security vulnerabilities.

#### Step 1: Create the Project

```bash
# Check your SDK version (must be 10.x)
dotnet --version

# Create Web API project with controllers
dotnet new webapi -n TmsApi --no-openapi --use-controllers

# Navigate to project
cd TmsApi

# Open in VS Code
code .
```

#### Step 2: Create the Broken Pipeline

**File**: `Program.cs`
```csharp
// Starter pipeline (BROKEN - middleware in wrong order)
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseRouting();

// PROBLEM: Endpoint executes BEFORE authentication!
app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001",
    letterGrade = "A"
}));

// PROBLEM: Authentication comes AFTER endpoint - too late!
app.UseAuthentication();
app.UseAuthorization();

app.Run();
```

#### Step 3: Test the Broken Version

```bash
dotnet run
```

**Expected**: App crashes with missing services error.

#### Step 4: Add Missing Services

**File**: `Program.cs`
```csharp
// Add missing services
var builder = WebApplication.CreateBuilder(args);

// ADD: Required services
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();
// ... rest same as above
```

#### Step 5: Create Training Authentication Handler

**File**: `TrainingAuthHandler.cs`
```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public class TrainingAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TrainingAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Training-User"))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Missing training user header."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, Request.Headers["X-Training-User"]!)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

#### Step 6: Fix the Pipeline Order

**File**: `Program.cs`
```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// FIXED: Add authentication with Training scheme
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
builder.Services.AddAuthorization();

var app = builder.Build();

// CORRECT ORDER: Middleware before endpoints
app.UseRouting();
app.UseAuthentication();  // BEFORE endpoints
app.UseAuthorization();   // BEFORE endpoints

// FIXED: Endpoint with authorization required
app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001",
    letterGrade = "A"
})).RequireAuthorization(); // Require authorization!

app.MapControllers();
app.Run();
```

#### Step 7: Test Both Scenarios

```bash
# Test anonymous request (should get 401)
curl http://localhost:5036/api/assessments/results

# Test authenticated request (should get JSON)
curl -H "X-Training-User: john.doe" http://localhost:5036/api/assessments/results
```

**Expected Results**:
- Anonymous: `401 Unauthorized`
- Authenticated: `200 OK` with JSON data

---

### Exercise 1B: Custom Request Logging Middleware

**Goal**: Add request logging with correlation IDs for request tracing.

#### Step 1: Create Request Logging Middleware

**File**: `RequestLoggingMiddleware.cs`
```csharp
using System.Diagnostics;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Generate short correlation ID
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        
        // Set response header BEFORE calling next (critical timing!)
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        
        // Start timing
        var stopwatch = Stopwatch.StartNew();
        
        // Log entry message
        _logger.LogInformation(
            "[{CorrelationId}] Request {Method} {Path}", 
            correlationId, 
            context.Request.Method, 
            context.Request.Path);
        
        await _next(context); // Pass control to next middleware/endpoint
        
        // Log completion message with timing
        stopwatch.Stop();
        _logger.LogInformation(
            "[{CorrelationId}] Completed {StatusCode} in {ElapsedMs}ms",
            correlationId,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds);
    }
}
```

#### Step 2: Register Middleware in Correct Order

**File**: `Program.cs` - Update middleware registration:
```csharp
var app = builder.Build();

// EXERCISE 1B: Middleware in the specified order
app.UseMiddleware<RequestLoggingMiddleware>(); // FIRST - outer wrapper
app.UseExceptionHandler("/Error");            // Exception handling
app.UseHttpsRedirection();                    // HTTPS redirect
app.UseRouting();                            // Routing
app.UseAuthentication();                     // Authentication
app.UseAuthorization();                      // Authorization

// Map endpoints...
```

#### Step 3: Test Request Logging

```bash
dotnet run

# Test any endpoint
curl http://localhost:5036/api/assessments/results
```

**Expected Console Output**:
```
[a1b2c3d4] Request GET /api/assessments/results
Training was not authenticated. Failure message: Missing training user header.
[a1b2c3d4] Completed 401 in 15ms
```

**Expected Response Headers**:
```
X-Correlation-Id: a1b2c3d4
```

---

## SESSION 2: Services Done Right

### Prerequisites Check

Before starting Session 2, verify your project has:
- ✅ Program.cs with canonical pipeline order
- ✅ RequestLoggingMiddleware.cs registered first
- ✅ Working GET /api/assessments/results endpoint returning 401 for anonymous calls
- ✅ X-Correlation-Id header on every response

### TMS Service Foundation

#### Step 1: Create Enrollment Service

**File**: `EnrollmentService.cs`
```csharp
// --- The contract (interface) ---
public interface IEnrollmentService
{
    Task<EnrollmentRecord> EnrollAsync(string studentId, string courseCode);
    Task<EnrollmentRecord?> GetByIdAsync(string id);
    Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync();
    Task<bool> DeleteAsync(string id);
}

// --- The in-memory implementation ---
public class EnrollmentService : IEnrollmentService
{
    private readonly Dictionary<string, EnrollmentRecord> _store = new();
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(ILogger<EnrollmentService> logger)
    {
        _logger = logger;
    }

    public Task<EnrollmentRecord> EnrollAsync(string studentId, string courseCode)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var record = new EnrollmentRecord(id, studentId, courseCode, DateTime.UtcNow);
        _store[id] = record;
        
        _logger.LogInformation(
            "Enrolled {StudentId} in {CourseCode} record {EnrollmentId}",
            studentId, courseCode, id);
        
        return Task.FromResult(record);
    }

    public Task<EnrollmentRecord?> GetByIdAsync(string id)
    {
        _store.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync()
    {
        IReadOnlyList<EnrollmentRecord> all = _store.Values.ToList();
        return Task.FromResult(all);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var removed = _store.Remove(id);
        return Task.FromResult(removed);
    }
}

// --- The data shape (record type) ---
public record EnrollmentRecord(
    string Id, 
    string StudentId, 
    string CourseCode, 
    DateTime EnrolledAt);
```

---

### Exercise 2: The Memory Leak (Captive Dependencies)

**Goal**: Learn about DI lifetime problems and fix captive dependencies.

#### Step 1: Create Buggy Enrollment Worker

**File**: `EnrollmentWorker.cs`
```csharp
// BUGGY VERSION - This will cause captive dependency error!
public class EnrollmentWorker
{
    private readonly IEnrollmentService _enrollmentService;
    
    // PROBLEM: Singleton constructor takes Scoped service directly
    public EnrollmentWorker(IEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }
    
    public void ProcessBatch()
    {
        // Simulate background work - recalculating scholarships
        var enrollments = _enrollmentService.GetAllAsync().Result;
        Console.WriteLine($"Processing {enrollments.Count} enrollments for scholarship recalculation");
        
        foreach (var enrollment in enrollments)
        {
            Console.WriteLine($"Recalculating scholarship for student {enrollment.StudentId} in course {enrollment.CourseCode}");
        }
    }
}
```

#### Step 2: Register Services with Problematic Lifetimes

**File**: `Program.cs` - Add these registrations:
```csharp
// EXERCISE 2: Buggy DI registration (will cause captive dependency error)
builder.Services.AddSingleton<EnrollmentWorker>();           // Singleton
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>(); // Scoped - PROBLEM!

// Add validation to catch the error early
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});
```

#### Step 3: Add Test Endpoint

**File**: `Program.cs` - Add after other endpoints:
```csharp
// Test route to trigger the captive dependency
app.MapGet("/api/enrollments/worker-smoke", (EnrollmentWorker worker) =>
{
    worker.ProcessBatch();
    return Results.Ok("processed");
});
```

#### Step 4: Test the Broken Version

```bash
dotnet run
```

**Expected Error**:
```
Cannot consume scoped service 'IEnrollmentService' from singleton 'EnrollmentWorker'
```

**This error is GOOD - it's protecting you from the captive dependency bug!**

#### Step 5: Fix Using IServiceScopeFactory

**File**: `EnrollmentWorker.cs` - Replace with fixed version:
```csharp
// FIXED VERSION - Uses IServiceScopeFactory to avoid captive dependency
public class EnrollmentWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    // SOLUTION: Inject the scope factory, not the scoped service
    public EnrollmentWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
    
    public void ProcessBatch()
    {
        // SOLUTION: Create a short-lived scope for this operation
        using var scope = _scopeFactory.CreateScope();
        
        // SOLUTION: Get the scoped service from the temporary scope
        var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
        
        // Now use the service safely
        var enrollments = enrollmentService.GetAllAsync().Result;
        Console.WriteLine($"Processing {enrollments.Count} enrollments for scholarship recalculation");
        
        foreach (var enrollment in enrollments)
        {
            Console.WriteLine($"Recalculating scholarship for student {enrollment.StudentId} in course {enrollment.CourseCode}");
        }
        
        // The 'using' automatically disposes the scope and its services
    }
}
```

#### Step 6: Test the Fixed Version

```bash
dotnet run

# Test the worker endpoint
curl http://localhost:5036/api/enrollments/worker-smoke
```

**Expected**: App starts successfully, endpoint returns "processed".

---

### Exercise 3: The Silent Crash (Options Pattern)

**Goal**: Validate configuration at startup instead of runtime failures.

#### Step 1: Create Payment Options Class

**File**: `PaymentOptions.cs`
```csharp
using System.ComponentModel.DataAnnotations;

public class PaymentOptions
{
    [Required]
    public required string GatewayUrl { get; init; }
    
    [Range(100, 100000)]
    public decimal MaxDepositBirr { get; init; }
}
```

#### Step 2: Add Configuration Section

**File**: `appsettings.json` - Add Payments section:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Payments": {
    "GatewayUrl": "https://payment-gateway.ethiopia.gov.et",
    "MaxDepositBirr": 50000
  }
}
```

#### Step 3: Register Options with Validation

**File**: `Program.cs` - Add options configuration:
```csharp
// EXERCISE 3: Configure PaymentOptions with startup validation
builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart(); // This makes it crash at startup if config is invalid
```

#### Step 4: Test with Valid Configuration

```bash
dotnet run
```

**Expected**: App starts successfully.

#### Step 5: Test with Invalid Configuration

Remove the Payments section from `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

```bash
dotnet run
```

**Expected Error**:
```
DataAnnotation validation failed for 'PaymentOptions': 'The GatewayUrl field is required.'
```

#### Step 6: Restore Valid Configuration

Put back the Payments section to make the app work again.

---

### Exercise 4: The Unsearchable Logs (Structured Logging)

**Goal**: Implement structured logging for production debugging.

#### Step 1: Update EnrollmentService with Structured Logging

**File**: `EnrollmentService.cs` - Replace methods with improved versions:

```csharp
public Task<EnrollmentRecord> EnrollAsync(string studentId, string courseCode)
{
    // Check for duplicate enrollment first
    var existing = _store.Values
        .FirstOrDefault(e => e.StudentId == studentId && e.CourseCode == courseCode);
    
    if (existing is not null)
    {
        // GOOD: Structured logging with proper log level
        _logger.LogWarning(
            "Duplicate enrollment attempt {StudentId} already in {CourseCode} (record {EnrollmentId})",
            studentId, courseCode, existing.Id);
        return Task.FromResult(existing);
    }
    
    var id = Guid.NewGuid().ToString("N")[..8];
    var record = new EnrollmentRecord(id, studentId, courseCode, DateTime.UtcNow);
    _store[id] = record;
    
    // GOOD: Structured logging - StudentId, CourseCode, EnrollmentId become searchable properties
    _logger.LogInformation(
        "Enrolled {StudentId} in {CourseCode} record {EnrollmentId}",
        studentId, courseCode, id);
    
    return Task.FromResult(record);
}

public Task<EnrollmentRecord?> GetByIdAsync(string id)
{
    _store.TryGetValue(id, out var record);
    
    if (record is null)
    {
        // GOOD: Structured logging with appropriate log level
        _logger.LogWarning("Enrollment {EnrollmentId} not found", id);
    }
    
    return Task.FromResult(record);
}

public Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync()
{
    IReadOnlyList<EnrollmentRecord> all = _store.Values.ToList();
    
    // GOOD: Structured logging with count as searchable property
    _logger.LogInformation("Retrieved {EnrollmentCount} enrollment records", all.Count);
    
    return Task.FromResult(all);
}

public Task<bool> DeleteAsync(string id)
{
    var removed = _store.Remove(id);
    
    if (removed)
    {
        // GOOD: Information level for successful business operation
        _logger.LogInformation("Deleted enrollment {EnrollmentId}", id);
    }
    else
    {
        // GOOD: Warning level for expected but problematic condition
        _logger.LogWarning("Delete failed enrollment {EnrollmentId} not found", id);
    }
    
    return Task.FromResult(removed);
}
```

#### Step 2: Add Test Endpoint for Logging

**File**: `Program.cs` - Add test endpoint:
```csharp
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
```

#### Step 3: Test Structured Logging

```bash
dotnet run

# Test the logging endpoint
curl -X POST http://localhost:5036/api/enrollments/test
```

**Expected Console Output**:
```
[abc12345] Request POST /api/enrollments/test
info: EnrollmentService[0] Enrolled S-001 in CS-101 record 2ce34f3b
warn: EnrollmentService[0] Duplicate enrollment attempt S-001 already in CS-101 (record 2ce34f3b)
warn: EnrollmentService[0] Enrollment nonexistent not found
info: EnrollmentService[0] Retrieved 1 enrollment records
info: EnrollmentService[0] Deleted enrollment 2ce34f3b
warn: EnrollmentService[0] Delete failed enrollment 2ce34f3b not found
[abc12345] Completed 200 in 12ms
```

---

## Summary: What You Built

### 🏆 **Production-Ready Features**:
- ✅ **Secure API pipeline** - Authentication and authorization in correct order
- ✅ **Request tracing** - Every request has correlation ID for debugging
- ✅ **Memory safety** - No captive dependencies or memory leaks
- ✅ **Configuration validation** - App fails fast if critical config missing
- ✅ **Structured logging** - Searchable logs for production debugging

### 📁 **Files Created**:
1. **Program.cs** - Main application configuration
2. **RequestLoggingMiddleware.cs** - Request logging with correlation IDs
3. **TrainingAuthHandler.cs** - Custom authentication handler
4. **EnrollmentService.cs** - Business logic with structured logging
5. **EnrollmentWorker.cs** - Background worker using proper DI patterns
6. **PaymentOptions.cs** - Strongly-typed configuration with validation

### 🎯 **Key Patterns Learned**:
- **Middleware ordering** - Security middleware before endpoints
- **Structured logging** - `{PropertyName}` instead of string concatenation
- **Options pattern** - Type-safe configuration with validation
- **DI lifetimes** - Proper use of Singleton, Scoped, and Transient
- **Service scopes** - IServiceScopeFactory for singleton → scoped access
- **Correlation IDs** - Request tracing for production debugging

### 🚀 **Production Benefits**:
- **Fail fast** - Configuration problems caught at startup, not runtime
- **Debuggable** - Can trace any request through correlation IDs
- **Searchable** - Logs queryable by StudentId, CourseCode, etc.
- **Secure** - Anonymous users blocked from sensitive endpoints
- **Scalable** - No memory leaks or captive dependencies

This foundation supports enterprise-level applications with proper observability, security, and maintainability patterns.