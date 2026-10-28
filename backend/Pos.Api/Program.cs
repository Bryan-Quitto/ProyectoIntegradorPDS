using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pos.Api.Extensions;
using Pos.Api.Middleware;
using Pos.Application.DTOs;
using Pos.Application.UseCases;
using Pos.Infrastructure;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Security;

// 1. Cargar Variables de Entorno desde .env (RNF-09)
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}
else
{
    Env.Load();
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// 2. Registro de Servicios de Infraestructura y Aplicación
builder.Services.AddInfrastructureServices(builder.Configuration);

// 3. Configuración de Autenticación JWT Bearer
var jwtSecurityKey = builder.Configuration["JWT__SECURITYKEY"]
    ?? builder.Configuration["JWT_SECURITYKEY"]
    ?? "UnSecretoSuperSeguroParaElProyectoIntegradorPDS2026!";
var jwtIssuer = builder.Configuration["JWT__VALIDISSUER"]
    ?? builder.Configuration["JWT_VALIDISSUER"]
    ?? "PosApi";
var jwtAudience = builder.Configuration["JWT__VALIDAUDIENCE"]
    ?? builder.Configuration["JWT_VALIDAUDIENCE"]
    ?? "PosWebClient";

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

// Asegurar creación de Base de Datos SQLite y Seed inicial en arranque
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
    dbContext.Database.EnsureCreated();
}

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

// ==========================================
// ENDPOINTS DE LA API (Minimal APIs Delgadas)
// ==========================================

// Autenticación de Operadores Locales
app.MapPost("/api/auth/login", async (LoginRequest request, PosDbContext db, IJwtService jwtService) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "El usuario y contraseña son requeridos." });
    }

    var hash = PosDbContext.HashPassword(request.Password);
    var user = await db.LocalOperators
        .FirstOrDefaultAsync(u => u.Username == request.Username && u.PasswordHash == hash && u.IsActive);

    if (user == null)
    {
        return Results.Unauthorized();
    }

    var token = jwtService.GenerateToken(user.Username, user.FullName, user.Role);
    return Results.Ok(new
    {
        token,
        username = user.Username,
        fullName = user.FullName,
        role = user.Role
    });
}).AllowAnonymous();

// Búsqueda de Productos en Odoo
app.MapGet("/api/products", async (string? search, SearchProductsUseCase useCase, CancellationToken ct) =>
{
    var products = await useCase.ExecuteAsync(search ?? string.Empty, ct);
    return Results.Ok(products);
}).AllowAnonymous();

// Consulta de Sesión de POS Activa
app.MapGet("/api/pos/sessions/active", async (GetActiveSessionUseCase useCase, CancellationToken ct) =>
{
    var session = await useCase.ExecuteAsync(ct);
    return session != null ? Results.Ok(session) : Results.NotFound(new { message = "No hay ninguna sesión de POS abierta en Odoo actualmente." });
}).AllowAnonymous();

// Registro de Venta con Idempotencia Obligatoria (RN-08)
app.MapPost("/api/sales", async (
    HttpContext httpContext,
    CreateSaleCommand command,
    CreateSaleUseCase useCase,
    CancellationToken ct) =>
{
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

    var effectiveCommand = command with { IdempotencyKey = idempotencyHeader };
    var result = await useCase.ExecuteAsync(effectiveCommand, ct);
    return Results.Created($"/api/sales/{result.OrderId}", result);
}).AllowAnonymous();

// Historial Local de Ventas
app.MapGet("/api/sales", async (GetSalesHistoryUseCase useCase, CancellationToken ct) =>
{
    var sales = await useCase.ExecuteAsync(ct);
    return Results.Ok(sales);
}).AllowAnonymous();

// Consulta de Venta por ID
app.MapGet("/api/sales/{id:guid}", async (Guid id, GetSaleByIdUseCase useCase, CancellationToken ct) =>
{
    var sale = await useCase.ExecuteAsync(id, ct);
    return sale != null ? Results.Ok(sale) : Results.NotFound();
}).AllowAnonymous();

// Health Check
app.MapGet("/health", () => Results.Ok(new
{
    status = "UP",
    service = "Pos.Api (.NET 10)",
    timestamp = DateTime.UtcNow
}));

app.Run();

public record LoginRequest(string Username, string Password);
