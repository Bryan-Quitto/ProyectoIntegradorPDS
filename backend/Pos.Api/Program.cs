using System.Security.Claims;
using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Pos.Api.Extensions;
using Pos.Api.Middleware;
using Pos.Application.DTOs;
using Pos.Application.UseCases;
using Pos.Infrastructure;

// 1. Cargar Variables de Entorno desde .env (RNF-09)
var candidatePaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env"),
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env")
};
var loaded = false;
foreach (var path in candidatePaths)
{
    var full = Path.GetFullPath(path);
    if (File.Exists(full))
    {
        Env.Load(full);
        loaded = true;
        break;
    }
}
if (!loaded)
{
    Env.Load();
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// 2. Registro de Servicios de Infraestructura y Aplicación
builder.Services.AddInfrastructureServices(builder.Configuration);

// 3. Configuración de Autenticación JWT Bearer
// [CRÍTICO]: Usar la misma clave configurada en JwtOptions desde el DI para evitar discrepancias
string jwtSecurityKey;
string jwtIssuer;
string jwtAudience;

using (var tempSp = builder.Services.BuildServiceProvider())
{
    var jwtOpts = tempSp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Pos.Infrastructure.Security.JwtOptions>>().Value;
    jwtSecurityKey = jwtOpts.SecurityKey;
    jwtIssuer = jwtOpts.ValidIssuer;
    jwtAudience = jwtOpts.ValidAudience;
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecurityKey))
        };
    });

builder.Services.AddAuthorization();

// 4. Configuración de CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowViteApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Idempotency-Key", "X-Server-Instance");
    });
});

// 5. Swagger con soporte Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGenBearer();

var app = builder.Build();

// Inicialización de la persistencia local SQLite (Audit & Idempotency)
app.Services.InitializePosDatabase();

// Middleware Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "POS Odoo 20 API v1");
    });
}

app.UseCors("AllowViteApp");
app.UseAuthentication();
app.UseAuthorization();

// ==============================================================
// ENDPOINTS DE LA API (Minimal APIs delgadas - Orquestación Pura)
// ==============================================================

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

// Búsqueda de Productos en Odoo
app.MapGet("/api/products", async (string? search, SearchProductsUseCase useCase, CancellationToken ct) =>
{
    var products = await useCase.ExecuteAsync(search ?? string.Empty, ct);
    return Results.Ok(products);
}).AllowAnonymous();

// Búsqueda de Clientes en Odoo (res.partner)
app.MapGet("/api/customers", async (string? search, SearchCustomersUseCase useCase, CancellationToken ct) =>
{
    var customers = await useCase.ExecuteAsync(search ?? string.Empty, ct);
    return Results.Ok(customers);
}).AllowAnonymous();

// Consulta de Sesión de POS Activa
app.MapGet("/api/pos/sessions/active", async (GetActiveSessionUseCase useCase, CancellationToken ct) =>
{
    var session = await useCase.ExecuteAsync(ct);
    return session != null ? Results.Ok(session) : Results.NotFound(new { message = "No hay ninguna sesión de POS abierta en Odoo actualmente." });
}).AllowAnonymous();

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

// Health Check
app.MapGet("/health", () => Results.Ok(new
{
    status = "UP",
    service = "Pos.Api (.NET 10)",
    timestamp = DateTime.UtcNow
}));

app.Run();
