# TmsApi — Project Restructuring & tms-core Integration

## What This Document Covers

This document explains two things we did:
1. Restructured `TmsApi` into the standard folder layout (Controllers / Middleware / Services / Models)
2. Connected `tms-core` and `TmsApi` so they share models instead of duplicating code

---

## Part 1 — Folder Restructuring

### The Problem Before

Every file was sitting in the project root with no organisation:

```
TmsApi/
├── EnrollmentService.cs        ← business logic
├── EnrollmentWorker.cs         ← background processor
├── EnrollmentsController.cs    ← HTTP surface
├── PaymentOptions.cs           ← configuration model
├── RequestLoggingMiddleware.cs ← pipeline middleware
├── TrainingAuthHandler.cs      ← auth middleware
├── WeatherForecast.cs          ← scaffold noise
├── Controllers/
│   └── WeatherForecastController.cs  ← scaffold noise
└── Program.cs
```

When a project has no folder structure, every file is equally important at first glance.
A new developer has no idea where to start. As the project grows from 8 files to 80,
finding anything becomes a search problem instead of a navigation problem.

### The Standard Structure We Applied

```
TmsApi/
├── Controllers/    ← HTTP surface only, one file per resource
├── Middleware/     ← pipeline cross-cutting concerns
├── Models/         ← data shapes shared across layers
├── Services/       ← business logic and interfaces
└── Program.cs      ← wires everything together
```

### What Moved Where and Why

**→ `Controllers/EnrollmentsController.cs`**
Already existed but was at the root. Moved into `Controllers/`.
Controllers belong here because they are the HTTP surface — they translate HTTP verbs
into service calls and translate service results into status codes. Nothing else.

**→ `Middleware/RequestLoggingMiddleware.cs`**
Moved from root. Middleware is a pipeline concern — it runs on every request regardless
of which endpoint is hit. It does not belong next to business logic.

**→ `Middleware/TrainingAuthHandler.cs`**
Moved from root. Authentication handlers are middleware-level concerns — they intercept
the request before it reaches any endpoint. Same folder as `RequestLoggingMiddleware`.

**→ `Services/EnrollmentService.cs`**
Moved from root. Services contain business logic — the rules of what the application
does. The interface `IEnrollmentService` lives in the same file as its implementation
because they are tightly related and small enough to not need separation yet.

**→ `Services/EnrollmentWorker.cs`**
Moved from root. Workers are background processors — they consume services.
They live in `Services/` because they are part of the business layer, not the HTTP layer.

**→ `Models/PaymentOptions.cs`**
Moved from root. Configuration classes are data shapes — they model external data
(in this case `appsettings.json`). They belong in `Models/`.

**Deleted:**
- `WeatherForecast.cs` — ASP.NET scaffold template noise, not part of TMS
- `Controllers/WeatherForecastController.cs` — same reason

### Namespaces Added

Every moved file got a namespace matching its folder:

| File | Namespace |
|------|-----------|
| `Controllers/EnrollmentsController.cs` | `TmsApi.Controllers` |
| `Middleware/RequestLoggingMiddleware.cs` | `TmsApi.Middleware` |
| `Middleware/TrainingAuthHandler.cs` | `TmsApi.Middleware` |
| `Services/EnrollmentService.cs` | `TmsApi.Services` |
| `Services/EnrollmentWorker.cs` | `TmsApi.Services` |
| `Models/PaymentOptions.cs` | `TmsApi.Models` |

Namespaces matter because they prevent name collisions. When `tms-core` and `TmsApi`
both had a class called `EnrollmentService`, the compiler could not tell them apart.
Namespaces give each one a full name: `TmsApi.Services.EnrollmentService` vs the
global `EnrollmentService` from `tms-core`. That is why `Program.cs` uses the fully
qualified name `TmsApi.Services.EnrollmentService` when registering the service.

### `Program.cs` After Restructuring

Added three using directives so the moved types resolve:

```csharp
using TmsApi.Middleware;   // RequestLoggingMiddleware, TrainingAuthHandler
using TmsApi.Models;       // PaymentOptions
using TmsApi.Services;     // IEnrollmentService, EnrollmentWorker
```

---

## Part 2 — Connecting tms-core and TmsApi

### The Problem Before

`tms-core` had `Course`, `Student`, `EnrollmentRecord`, `TmsDatabaseException` and
other models. `TmsApi` had its own copies of `EnrollmentRecord`, `CreateEnrollmentRequest`,
and `TmsDatabaseException` in `Models/EnrollmentRecord.cs`.

Two copies of the same model means two sources of truth. If you change `EnrollmentRecord`
in one place, the other is out of sync. You fix a bug twice. You explain the same type twice.

### Why You Cannot Just Reference a Console App

`tms-core` was an `Exe` project (`<OutputType>Exe</OutputType>`). .NET does not allow
referencing an executable as a library — an `Exe` produces a `.exe` file, not a `.dll`.
Only a `.dll` (class library) can be referenced by other projects.

### What We Changed in tms-core

**`TmsCore.csproj` — removed `OutputType`:**

```xml
<!-- Before -->
<OutputType>Exe</OutputType>

<!-- After — removed entirely, defaults to library -->
```

Removing `OutputType` makes .NET produce a `.dll` instead of a `.exe`. The project
becomes a class library that any other project can reference.

**`tms-core/Program.cs` — wrapped in a static class:**

The exercise code was top-level statements — code written directly at file level with
no class or method wrapper. Top-level statements are only valid in an executable entry
point. A class library has no entry point.

To preserve all the exercise code without deleting a single line, we wrapped everything
in a static class:

```csharp
// Before — top-level statements (only valid in Exe)
string? region = null;
Console.WriteLine(region);
// ... 200 more lines

// After — same code, wrapped in a class
public static class Exercises
{
    public static async Task RunAsync()
    {
        string? region = null;
        Console.WriteLine(region);
        // ... same 200 lines, untouched
    }
}
```

All exercises still exist. Nothing was deleted or changed inside the method.

**`tms-core/model.cs` — three additions:**

1. `Id` field added to `EnrollmentRecord`:
```csharp
// Before
public record EnrollmentRecord(string StudentId, string CourseCode, DateTime EnrolledAt);

// After
public record EnrollmentRecord(string Id, string StudentId, string CourseCode, DateTime EnrolledAt);
```
`TmsApi` needs `Id` on every enrollment so the controller can build the `Location` header
for `POST /api/enrollments`. The `tms-core` exercises did not need it before.

2. `CreateEnrollmentRequest` added:
```csharp
public record CreateEnrollmentRequest(string StudentId, string CourseCode);
```
This is the shape of the POST request body. It lives here now so both projects share it.

3. Single-arg constructor added to `TmsDatabaseException`:
```csharp
public TmsDatabaseException(string message) : base(message) { }
```
`TmsApi` throws this with just a message. The original only had a two-arg constructor
`(operation, message)`. Adding the single-arg constructor means both usages work.

**`tms-core/EnrollmentService.cs` — fixed constructor call:**
```csharp
// Before — 3 args (old EnrollmentRecord shape)
return new EnrollmentRecord(student.Id, course.Code, DateTime.UtcNow);

// After — 4 args (new shape with Id first)
return new EnrollmentRecord(Guid.NewGuid().ToString("N")[..8], student.Id, course.Code, DateTime.UtcNow);
```

### What We Changed in TmsApi

**`TmsApi.csproj` — added project reference:**

```xml
<ItemGroup>
    <ProjectReference Include="..\tms-core\TmsCore.csproj" />
</ItemGroup>
```

This one line is all .NET needs to make every public type in `tms-core` available in
`TmsApi`. No copying, no NuGet package — just a direct project-to-project reference.
When you build `TmsApi`, it automatically builds `tms-core` first.

**`TmsApi/Models/EnrollmentRecord.cs` — deleted:**

This file had `EnrollmentRecord`, `CreateEnrollmentRequest`, and `TmsDatabaseException`.
All three now live in `tms-core/model.cs`. Keeping both would cause a compiler error:
"The type already exists in another assembly." One source of truth — `tms-core` owns them.

**`TmsApi/Services/EnrollmentService.cs` — removed `using TmsApi.Models`:**

`EnrollmentRecord` is no longer in `TmsApi.Models`. It is now a global type (no namespace)
coming from `tms-core`. No using directive needed to access it.

**`TmsApi/Program.cs` — fully qualified `EnrollmentService`:**

```csharp
// Without full qualification — ambiguous: which EnrollmentService?
builder.Services.AddSingleton<IEnrollmentService, EnrollmentService>();

// With full qualification — unambiguous
builder.Services.AddSingleton<IEnrollmentService, TmsApi.Services.EnrollmentService>();
```

Both `tms-core` and `TmsApi.Services` have a class named `EnrollmentService`. The compiler
cannot guess which one you mean. The full namespace path removes all ambiguity.

---

## The Final Project Relationship

```
tms-core  (class library)
│
│   owns: EnrollmentRecord, CreateEnrollmentRequest
│         Course, Student, TmsDatabaseException
│         CapacityReachedException, IGradable, Quiz, LabAssignment
│         EnrollmentService (core/domain version)
│         Exercises (all lab exercise code)
│
└──► referenced by
         │
         TmsApi  (ASP.NET Web API)
         │
         │   owns: IEnrollmentService (API contract)
         │          TmsApi.Services.EnrollmentService (in-memory HTTP version)
         │          EnrollmentWorker, PaymentOptions
         │          RequestLoggingMiddleware, TrainingAuthHandler
         │          EnrollmentsController
         │
         └──► exposes HTTP endpoints to frontend (Angular in M8)
```

### Why Two EnrollmentService Classes

`tms-core` has an `EnrollmentService` that takes a `Student` and `Course` object and
applies business rules (capacity check, GPA standing). It is domain logic.

`TmsApi.Services.EnrollmentService` takes a `studentId` string and `courseCode` string,
stores records in an in-memory dictionary, and logs every operation. It is HTTP/API logic.

They do different things at different layers. Having both is correct — they are not
duplicates, they are different responsibilities at different levels of the stack.

---

## Files Changed — Complete Reference

| File | Change | Why |
|------|--------|-----|
| `tms-core/TmsCore.csproj` | Removed `<OutputType>Exe</OutputType>` | Makes it a referenceable class library |
| `tms-core/Program.cs` | Wrapped all code in `public static class Exercises` | Top-level statements not valid in a library |
| `tms-core/model.cs` | Added `Id` to `EnrollmentRecord`, added `CreateEnrollmentRequest`, added single-arg `TmsDatabaseException` constructor | TmsApi needs these shapes |
| `tms-core/EnrollmentService.cs` | Fixed `EnrollmentRecord` constructor call to include `Id` | Matches updated record shape |
| `TmsApi/TmsApi.csproj` | Added `<ProjectReference>` to tms-core | Enables sharing types across projects |
| `TmsApi/Models/EnrollmentRecord.cs` | Deleted | tms-core now owns these types |
| `TmsApi/Services/EnrollmentService.cs` | Removed `using TmsApi.Models` | Types are now global from tms-core |
| `TmsApi/Controllers/EnrollmentsController.cs` | Removed `using TmsApi.Models` | Same reason |
| `TmsApi/Program.cs` | Fully qualified `TmsApi.Services.EnrollmentService`, restored `using TmsApi.Models` for `PaymentOptions` | Resolves naming ambiguity |
