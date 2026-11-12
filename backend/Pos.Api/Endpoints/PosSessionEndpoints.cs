using Pos.Application.UseCases;

namespace Pos.Api.Endpoints;

public static class PosSessionEndpoints
{
    public static IEndpointRouteBuilder MapPosSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // Consulta de Sesión de POS Activa
        app.MapGet("/api/pos/sessions/active", async (GetActiveSessionUseCase useCase, CancellationToken ct) =>
        {
            var session = await useCase.ExecuteAsync(ct);
            return session != null 
                ? Results.Ok(session) 
                : Results.NotFound(new { message = "No hay ninguna sesión de POS abierta en Odoo actualmente." });
        }).AllowAnonymous();

        return app;
    }
}
