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
                FormatFriendlyOdooMessage(oce.Message),
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

    private static string FormatFriendlyOdooMessage(string? rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            return "No fue posible completar la operación con el servidor Odoo ERP.";

        if (rawMessage.Contains("Session user does not match", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("HTTP 403", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("denegó el acceso", StringComparison.OrdinalIgnoreCase))
        {
            return "El servidor Odoo denegó la operación por permisos o credenciales no válidas. Contacte al administrador.";
        }

        if (rawMessage.Contains("HTTP 502", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("HTTP 503", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("No fue posible conectar", StringComparison.OrdinalIgnoreCase))
        {
            return "No fue posible establecer comunicación con el servidor Odoo ERP. Verifique que el servicio esté activo.";
        }

        return rawMessage;
    }
}
