using Pos.Application.UseCases;

namespace Pos.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        // Búsqueda de Productos en Odoo
        app.MapGet("/api/products", async (string? search, SearchProductsUseCase useCase, CancellationToken ct) =>
        {
            var products = await useCase.ExecuteAsync(search ?? string.Empty, ct);
            return Results.Ok(products);
        }).AllowAnonymous();

        return app;
    }
}
