using Pos.Application.UseCases;

namespace Pos.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        // Búsqueda de Productos en Odoo con Soporte de Paginación Masiva (1M+) y Filtro por IDs
        app.MapGet("/api/products", async (
            HttpContext httpContext,
            string? search,
            int? page,
            int? pageSize,
            string? ids,
            string? criteria,
            SearchProductsUseCase useCase,
            GetProductsByIdsUseCase getProductsByIdsUseCase,
            CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(ids))
            {
                var parsedIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                   .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
                                   .Where(id => id.HasValue)
                                   .Select(id => id!.Value)
                                   .Distinct()
                                   .ToArray();

                var productsByIds = await getProductsByIdsUseCase.ExecuteAsync(parsedIds, ct);
                return Results.Ok(productsByIds);
            }

            if (page.HasValue || pageSize.HasValue)
            {
                var (products, totalCount) = await useCase.ExecutePagedAsync(
                    search ?? string.Empty,
                    page ?? 1,
                    pageSize ?? 8,
                    criteria,
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
