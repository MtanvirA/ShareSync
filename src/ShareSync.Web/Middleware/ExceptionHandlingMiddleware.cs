using System.Net;
using System.Text.Json;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Models;

namespace ShareSync.Web.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var statusCode = HttpStatusCode.InternalServerError;
        var message = "An internal server error occurred. Please try again later.";
        List<string>? errors = null;

        if (exception is AppException appEx)
        {
            statusCode = (HttpStatusCode)appEx.StatusCode;
            message = appEx.Message;
            _logger.LogWarning("Application exception ({StatusCode}): {Message}", appEx.StatusCode, appEx.Message);
        }
        else if (exception is UnauthorizedAccessException)
        {
            statusCode = HttpStatusCode.Unauthorized;
            message = "You are not authorized to access this resource.";
            _logger.LogWarning("Unauthorized access attempt.");
        }
        else if (exception is KeyNotFoundException)
        {
            statusCode = HttpStatusCode.NotFound;
            message = "The requested resource was not found.";
            _logger.LogWarning("Resource not found: {Message}", exception.Message);
        }
        else if (exception is InvalidOperationException invEx)
        {
            statusCode = HttpStatusCode.BadRequest;
            message = invEx.Message;
            _logger.LogWarning("Invalid operation: {Message}", invEx.Message);
        }
        else
        {
            _logger.LogError(exception, "An unhandled system exception occurred: {Message}", exception.Message);

            if (exception.GetType().FullName?.Contains("MySql") == true ||
                exception.GetType().FullName?.Contains("DbUpdate") == true)
            {
                statusCode = HttpStatusCode.BadRequest;
                message = "A database integrity constraint or operation error occurred.";
            }
        }

        response.StatusCode = (int)statusCode;
        var apiResponse = ApiResponse.Fail(message, errors);
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        await response.WriteAsync(JsonSerializer.Serialize(apiResponse, jsonOptions));
    }
}
