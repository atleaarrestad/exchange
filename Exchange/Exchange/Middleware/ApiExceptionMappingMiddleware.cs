using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Exchange.Middleware;

public sealed class ApiExceptionMappingMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMappingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (ApplicationValidationException exception)
        {
            await WriteValidationProblemAsync(context, StatusCodes.Status400BadRequest, exception.Errors);
        }
        catch (ArgumentException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid request",
                exception.Message);
        }
        catch (BlockchainTransferRejectedException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "Transfer rejected",
                exception.Message);
        }
        catch (BlockchainTransferTimeoutException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "Blockchain gateway timeout",
                exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception for request {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "An unexpected error occurred.");
        }
    }

    private static Task WriteValidationProblemAsync(
        HttpContext context,
        int statusCode,
        IReadOnlyDictionary<string, string[]> errors)
    {
        var problemDetails = new ValidationProblemDetails(errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal))
        {
            Status = statusCode,
            Title = "Validation failed",
            Type = "https://httpstatuses.com/400"
        };

        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(problemDetails);
    }
}
