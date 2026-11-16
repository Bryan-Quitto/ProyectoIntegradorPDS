using Pos.Application.UseCases;

namespace Pos.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        // Búsqueda de Productos en Odoo con Soporte de Paginación Masiva (1M+)
        app.MapGet("/api/products", async (
            HttpContext httpContext,
            string? search,
            int? page,
            int? pageSize,
            SearchProductsUseCase useCase,
            CancellationToken ct) =>
        {
            if (page.HasValue || pageSize.HasValue)
            {
                var (products, totalCount) = await useCase.ExecutePagedAsync(
                    search ?? string.Empty,
                    page ?? 1,
                    pageSize ?? 8,
                    ct);

                httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();
                return Results.Ok(products);
            }

            var legacy = await useCase.ExecuteAsync(search ?? string.Empty, ct);
            return Results.Ok(legacy);
        }).AllowAnonymous();

        return app;
    }
}
