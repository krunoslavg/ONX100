using Onx100.Api.Models;
using Onx100.Driver.Exceptions;

namespace Onx100.Api.Middleware;

public sealed class ApiExceptionMiddleware
{
    /******************** PRIVATE MEMBERS ********************/
    private readonly RequestDelegate next;
    private readonly ILogger<ApiExceptionMiddleware> logger;


    /******************** CONSTRUCTOR ********************/
    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }


    /******************** PUBLIC METHODS ********************/
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(context, exception);
        }
    }


    /******************** PRIVATE METHODS ********************/
    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        (int statusCode, string code) = exception switch
        {
            Onx100TimeoutException => (StatusCodes.Status504GatewayTimeout, "device_timeout"),
            Onx100CommandException => (StatusCodes.Status409Conflict, "device_command_error"),
            IOException => (StatusCodes.Status503ServiceUnavailable, "device_unavailable"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "invalid_device_state"),
            ArgumentException => (StatusCodes.Status400BadRequest, "invalid_argument"),
            _ => (StatusCodes.Status500InternalServerError, "internal_error")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled API exception.");
        }
        else
        {
            logger.LogWarning(exception, "API request failed with status code {StatusCode}.", statusCode);
        }

        ApiErrorResponse response = new ApiErrorResponse(code, exception.Message);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }
}