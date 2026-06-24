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
        catch (IdempotencyKeyConflictException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Idempotency conflict",
                exception.Message);
        }
        catch (IdempotencyOperationPendingException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Idempotency operation pending",
                exception.Message);
        }
        catch (InsufficientFundsException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "Insufficient funds",
                exception.Message);
        }
        catch (PriceProtectionExceededException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "Price protection exceeded",
                exception.Message);
        }
        catch (QuoteExecutionRejectedException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "Quote execution rejected",
                exception.Message);
        }
        catch (ExternalHedgeExecutionUnavailableException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "External hedge execution unavailable",
                exception.Message);
        }
        catch (ExternalDependencyNotConfiguredException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "Dependency not configured",
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
