using Pos.Application.DTOs;
using Pos.Application.UseCases;

namespace Pos.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Autenticación de Operadores Locales (100% Desacoplado vía Use Case)
        app.MapPost("/api/auth/login", async (LoginCommand command, LoginOperatorUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(command, ct);
            if (result == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Credenciales Inválidas",
                    detail: "Usuario o contraseña incorrectos. Verifique sus credenciales e intente nuevamente."
                );
            }
            return Results.Ok(result);
        }).AllowAnonymous();

        return app;
    }
}
