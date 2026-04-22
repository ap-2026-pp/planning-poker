using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.Exceptions;

namespace PlanningPoker.API.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(
            exception, "Exception occurred: {Message}", exception.Message);

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

        await httpContext.Response.WriteAsJsonAsync(context, cancellationToken);
        return true;
    }
}