using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Exceptions;
using Pos.Domain.Exceptions;

namespace Pos.Api.Middleware;

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
            _logger.LogError(ex, "Excepción capturada en middleware global: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title, detail, type) = exception switch
        {
            DomainValidationException dve => (
                StatusCodes.Status400BadRequest,
                "Error de Validación de Dominio",
                dve.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            ),
            ApplicationValidationException ave => (
                StatusCodes.Status400BadRequest,
                "Error de Validación de Negocio",
                ave.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            ),
            SaleOrderStateException sose => (
                StatusCodes.Status422UnprocessableEntity,
                "Transición de Estado Inválida (RN-06)",
                sose.Message,
                "https://tools.ietf.org/html/rfc4918#section-11.2"
            ),
            OdooCommunicationException oce => (
                StatusCodes.Status502BadGateway,
                "Fallo de Comunicación con ERP Odoo (RN-07)",
                oce.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.6.3"
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Error Interno del Servidor",
                "Ocurrió un error inesperado al procesar la solicitud.",
                "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            )
        };

        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type,
            Instance = context.Request.Path
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
    }
}
