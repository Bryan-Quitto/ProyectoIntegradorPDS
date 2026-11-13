using Pos.Application.UseCases;

namespace Pos.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        // Búsqueda de Clientes en Odoo (res.partner)
        // [GUÍA ESTEBAN - MEJORA COLABORATIVA DE SEGURIDAD DEFENSIVA]:
        // Complementando los endpoints creados por Eli, protegemos /api/customers
        // con .RequireAuthorization() para asegurar que la consulta de clientes Odoo
        // requiera sesión activa de cajero o administrador (Principio de Menor Privilegio).
        app.MapGet("/api/customers", async (string? search, SearchCustomersUseCase useCase, CancellationToken ct) =>
        {
            var customers = await useCase.ExecuteAsync(search ?? string.Empty, ct);
            return Results.Ok(customers);
        }).RequireAuthorization();

        return app;
    }
}
