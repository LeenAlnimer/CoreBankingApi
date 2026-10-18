using System.Globalization;
using System.Text.Json;
using BankTask.Application.DTOs.Errors;
using BankTask.Application.Exceptions;
using BankTask.Application.Resources;

namespace BankTask.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (AppException ex)
        {
            var traceId = context.TraceIdentifier;

            _logger.LogWarning(
                ex,
                "Application exception occurred. ErrorCode: {ErrorCode} TraceId: {TraceId}",
                ex.ErrorCode,
                traceId);

            await HandleAppExceptionAsync(
                context,
                ex,
                traceId);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;

            _logger.LogError(
                ex,
                "Unhandled exception occurred. TraceId: {TraceId}",
                traceId);

            await HandleUnexpectedExceptionAsync(
                context,
                traceId);
        }
    }

    private static async Task HandleAppExceptionAsync(
        HttpContext context,
        AppException exception,
        string traceId)
    {
        context.Response.StatusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.ContentType = "application/json";

        var culture = GetCulture(context);

        var message = GetLocalizedMessage(
            exception.ErrorCode,
            culture);

        var response = new ErrorResponse
        {
            ErrorCode = exception.ErrorCode,
            Message = message,
            TraceId = traceId
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private static async Task HandleUnexpectedExceptionAsync(
        HttpContext context,
        string traceId)
    {
        context.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        context.Response.ContentType = "application/json";

        var culture = GetCulture(context);

        var message = GetLocalizedMessage(
            "UNEXPECTED_ERROR",
            culture);

        var response = new ErrorResponse
        {
            ErrorCode = "UNEXPECTED_ERROR",
            Message = message,
            TraceId = traceId
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private static string GetLocalizedMessage(
        string errorCode,
        CultureInfo culture)
    {
        return Errors.ResourceManager.GetString(
                   errorCode,
                   culture)
               ?? Errors.ResourceManager.GetString(
                   "UNEXPECTED_ERROR",
                   culture)!;
    }

    private static CultureInfo GetCulture(HttpContext context)
    {
        var acceptLanguage =
            context.Request.Headers.AcceptLanguage.ToString();

        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            return CultureInfo.GetCultureInfo("en");
        }

        var language =
            acceptLanguage
                .Split(',')
                .FirstOrDefault()?
                .Trim();

        if (!string.IsNullOrWhiteSpace(language) &&
            language.StartsWith(
                "ar",
                StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("ar");
        }

        return CultureInfo.GetCultureInfo("en");
    }
}

