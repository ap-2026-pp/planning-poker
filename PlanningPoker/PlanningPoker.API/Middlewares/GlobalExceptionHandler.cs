using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.Exceptions;

namespace PlanningPoker.API.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (404, "The requested resource was not found"),
            BadHttpRequestException => (400, "Bad Request"),
            _ => (500, "Internal Server Error")
        };

        httpContext.Response.StatusCode = status;
        
        var context = new ProblemDetails
        {
            Status = status,
            Type = exception.GetType().Name,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        if (status >= 500)
        {
            logger.LogError(
                exception,
                "Unhandled exception {ExceptionType} for {Method} {Path}. StatusCode: {StatusCode}. Message: {Message}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                status,
                exception.Message);
        }
        else
        {
            logger.LogWarning(
                "Handled exception {ExceptionType} for {Method} {Path}. StatusCode: {StatusCode}. Message: {Message}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                status,
                exception.Message);
        }
        
        await httpContext.Response.WriteAsJsonAsync(context, cancellationToken);
        return true;
    }
}
