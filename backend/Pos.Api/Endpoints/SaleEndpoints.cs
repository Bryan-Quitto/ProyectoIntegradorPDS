using System.Security.Claims;
using Pos.Application.DTOs;
using Pos.Application.UseCases;

namespace Pos.Api.Endpoints;

public static class SaleEndpoints
{
    public static IEndpointRouteBuilder MapSaleEndpoints(this IEndpointRouteBuilder app)
    {
        // Registro de Venta con Idempotencia Obligatoria (RN-08) y Validación de Caja Asignada (Fase 4)
        app.MapPost("/api/sales", async (
            HttpContext httpContext,
            CreateSaleCommand command,
            CreateSaleUseCase useCase,
            CancellationToken ct) =>
        {
            // Validación de autenticación
            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            // Validación de sesión/caja autorizada según token JWT
            var sessionClaim = httpContext.User.FindFirst("pos_session_id")?.Value;
            if (string.IsNullOrWhiteSpace(sessionClaim) || !int.TryParse(sessionClaim, out var authorizedSessionId) || authorizedSessionId <= 0)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Sin Caja Asignada",
                    detail: "El operador autenticado no tiene una caja o sesión de POS activa asignada en Odoo ERP. No está autorizado para registrar ventas."
                );
            }
            if (command.PosSessionId != authorizedSessionId)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Caja No Autorizada",
                    detail: $"El operador no tiene autorización para emitir ventas en la caja/sesión #{command.PosSessionId}. Su caja asignada es #{authorizedSessionId}."
                );
            }

            var idempotencyHeader = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault()
                ?? httpContext.Request.Headers["X-Idempotency-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(idempotencyHeader))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Header Requerido Faltante",
                    detail: "El encabezado 'Idempotency-Key' es obligatorio para procesar la venta (RN-08 / RF-16)."
                );
            }

            var odooUserIdClaim = httpContext.User.FindFirst("odoo_user_id")?.Value;
            int? authenticatedUserId = int.TryParse(odooUserIdClaim, out var parsedUid) && parsedUid > 0 ? parsedUid : null;

            var operatorNameClaim = httpContext.User.FindFirst("FullName")?.Value
                ?? httpContext.User.FindFirst(ClaimTypes.Name)?.Value
                ?? httpContext.User.FindFirst("name")?.Value;

            var effectiveCommand = command with
            {
                IdempotencyKey = idempotencyHeader,
                UserId = command.UserId ?? authenticatedUserId,
                OperatorName = !string.IsNullOrWhiteSpace(command.OperatorName) ? command.OperatorName : operatorNameClaim
            };
            var result = await useCase.ExecuteAsync(effectiveCommand, ct);
            return Results.Created($"/api/sales/{result.OrderId}", result);
        }).RequireAuthorization();

        // Historial Local de Ventas con Aislamiento por Caja y Rol (Least Privilege & Multi-cashier Data Isolation)
        app.MapGet("/api/sales", async (
            HttpContext httpContext,
            GetSalesHistoryUseCase useCase,
            CancellationToken ct) =>
        {
            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var isAdmin = httpContext.User.IsInRole("Administrator") ||
                          httpContext.User.Claims.Any(c =>
                              (c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                              && string.Equals(c.Value, "Administrator", StringComparison.OrdinalIgnoreCase));

            if (isAdmin)
            {
                var allSales = await useCase.ExecuteAsync(null, ct);
                return Results.Ok(allSales);
            }

            // Cajero / Vendedor: aislar por caja asignada en token
            var sessionClaim = httpContext.User.FindFirst("pos_session_id")?.Value;
            if (string.IsNullOrWhiteSpace(sessionClaim) || !int.TryParse(sessionClaim, out var authorizedSessionId) || authorizedSessionId <= 0)
            {
                // Sin caja asignada (modo consulta / cajero sin sesión): lista vacía
                return Results.Ok(Array.Empty<SaleOrderResponseDto>());
            }

            var sales = await useCase.ExecuteAsync(authorizedSessionId, ct);
            return Results.Ok(sales);
        }).RequireAuthorization();

        // Consulta de Venta por ID (RF-14) con Aislamiento por Caja y Rol
        app.MapGet("/api/sales/{id}", async (
            string id,
            HttpContext httpContext,
            GetSaleByIdUseCase useCase,
            CancellationToken ct) =>
        {
            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var sale = await useCase.ExecuteAsync(id, ct);
            if (sale == null)
            {
                return Results.NotFound(new { message = $"No se encontró la venta con identificador '{id}'." });
            }

            var isAdmin = httpContext.User.IsInRole("Administrator") ||
                          httpContext.User.Claims.Any(c =>
                              (c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                              && string.Equals(c.Value, "Administrator", StringComparison.OrdinalIgnoreCase));

            if (isAdmin)
            {
                return Results.Ok(sale);
            }

            var sessionClaim = httpContext.User.FindFirst("pos_session_id")?.Value;
            if (string.IsNullOrWhiteSpace(sessionClaim) || !int.TryParse(sessionClaim, out var authorizedSessionId) || authorizedSessionId <= 0 || sale.PosSessionId != authorizedSessionId)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Acceso Denegado",
                    detail: "No tiene permiso para ver detalles de órdenes pertenecientes a otra caja."
                );
            }

            return Results.Ok(sale);
        }).RequireAuthorization();

        return app;
    }
}
