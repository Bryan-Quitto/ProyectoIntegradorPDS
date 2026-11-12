using System.Security.Claims;
using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Pos.Api.Endpoints;
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
// Resuelto para RNF-04 / RNF-05: Lectura directa desde Configuration sin invocar BuildServiceProvider (Eliminación warning ASP0000)
var jwtOptions = new Pos.Infrastructure.Security.JwtOptions();
builder.Configuration.GetSection(Pos.Infrastructure.Security.JwtOptions.SectionKey).Bind(jwtOptions);

var envKey = builder.Configuration["JWT__SECURITYKEY"] ?? builder.Configuration["JWT_SECURITYKEY"];
if (!string.IsNullOrWhiteSpace(envKey)) jwtOptions.SecurityKey = envKey;

var envIssuer = builder.Configuration["JWT__VALIDISSUER"] ?? builder.Configuration["JWT_VALIDISSUER"];
if (!string.IsNullOrWhiteSpace(envIssuer)) jwtOptions.ValidIssuer = envIssuer;

var envAudience = builder.Configuration["JWT__VALIDAUDIENCE"] ?? builder.Configuration["JWT_VALIDAUDIENCE"];
if (!string.IsNullOrWhiteSpace(envAudience)) jwtOptions.ValidAudience = envAudience;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.ValidIssuer,
            ValidAudience = jwtOptions.ValidAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecurityKey))
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
// ENDPOINTS DE LA API (Modularizados en Pos.Api/Endpoints/ - SRP)
// ==============================================================
app.MapAuthEndpoints();
app.MapProductEndpoints();
app.MapCustomerEndpoints();
app.MapPosSessionEndpoints();
app.MapSaleEndpoints();

// Health Check
app.MapGet("/health", () => Results.Ok(new
{
    status = "UP",
    service = "Pos.Api (.NET 10)",
    timestamp = DateTime.UtcNow
}));

app.Run();
