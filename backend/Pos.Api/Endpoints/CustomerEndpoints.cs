using Pos.Application.UseCases;

namespace Pos.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        // Búsqueda de Clientes en Odoo (res.partner)
        app.MapGet("/api/customers", async (string? search, SearchCustomersUseCase useCase, CancellationToken ct) =>
        {
            var customers = await useCase.ExecuteAsync(search ?? string.Empty, ct);
            return Results.Ok(customers);
        }).AllowAnonymous();

        return app;
    }
}
