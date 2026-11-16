using Pos.Application.UseCases;

namespace Pos.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        // Búsqueda de Clientes en Odoo (res.partner) con Soporte de Paginación Masiva (1M+)
        // [GUÍA ESTEBAN - MEJORA COLABORATIVA DE SEGURIDAD DEFENSIVA]:
        // Protegido con .RequireAuthorization() para asegurar que la consulta de clientes Odoo
        // requiera sesión activa de cajero o administrador (Principio de Menor Privilegio).
        app.MapGet("/api/customers", async (
            HttpContext httpContext,
            string? search,
            int? page,
            int? pageSize,
            string? criteria,
            SearchCustomersUseCase useCase,
            CancellationToken ct) =>
        {
            if (page.HasValue || pageSize.HasValue)
            {
                var (customers, totalCount) = await useCase.ExecutePagedAsync(
                    search ?? string.Empty,
                    page ?? 1,
                    pageSize ?? 8,
                    criteria,
                    ct);

                httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();
                return Results.Ok(customers);
            }

            var legacy = await useCase.ExecuteAsync(search ?? string.Empty, ct);
            return Results.Ok(legacy);
        }).RequireAuthorization();

        return app;
    }
}
