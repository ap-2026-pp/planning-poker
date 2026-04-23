using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.Exceptions;

namespace PlanningPoker.API.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            GameAlreadyExistsException => (StatusCodes.Status409Conflict, "Game already exists"),
            NotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad Request"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        httpContext.Response.StatusCode = statusCode;
        
        var context = new ProblemDetails
        {
            Status = statusCode,
            Type = exception.GetType().Name,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        if (statusCode >= 500)
        {
            logger.LogError(
                exception,
                "Unhandled exception {ExceptionType} for {Method} {Path}. StatusCode: {StatusCode}. Message: {Message}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                exception.Message);
        }
        else
        {
            logger.LogWarning(
                "Handled exception {ExceptionType} for {Method} {Path}. StatusCode: {StatusCode}. Message: {Message}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                exception.Message);
        }
        
        await httpContext.Response.WriteAsJsonAsync(context, cancellationToken);
        return true;
    }
}
