using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prepared.Client.Middleware;

/// <summary>
/// Global error handling middleware with structured error responses and correlation ID tracking.
/// Ensures all exceptions are caught, logged, and returned in a consistent format.
/// </summary>
public class ErrorHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlerMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ErrorHandlerMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlerMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.TraceIdentifier;
            _logger.LogError(ex,
                "Unhandled exception occurred. CorrelationId={CorrelationId}, Path={Path}, Method={Method}",
                correlationId, context.Request.Path, context.Request.Method);

            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var isDevelopment = _environment.IsDevelopment();
        var response = new
        {
            error = new
            {
                message = "An error occurred while processing your request.",
                correlationId,
                timestamp = DateTime.UtcNow,
                path = context.Request.Path.Value,
                method = context.Request.Method,
                details = isDevelopment ? exception.Message : null,
                stackTrace = isDevelopment ? exception.StackTrace : null,
                innerException = isDevelopment && exception.InnerException != null
                    ? new { message = exception.InnerException.Message }
                    : null
            }
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        await context.Response.WriteAsync(json);
    }
}

