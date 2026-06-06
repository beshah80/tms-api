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
        // Step 1: Generate short correlation ID
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        
        // Step 2: Set response header BEFORE calling next (critical timing!)
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        
        // Step 3: Start timing
        var stopwatch = Stopwatch.StartNew();
        
        // Step 4: Log entry message
        _logger.LogInformation(
            "[{CorrelationId}] Request {Method} {Path}", 
            correlationId, 
            context.Request.Method, 
            context.Request.Path);
        
        await _next(context); // Pass control to next middleware/endpoint
        
        // Step 5: Log completion message with timing
        stopwatch.Stop();
        _logger.LogInformation(
            "[{CorrelationId}] Completed {StatusCode} in {ElapsedMs}ms",
            correlationId,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds);
    }
}