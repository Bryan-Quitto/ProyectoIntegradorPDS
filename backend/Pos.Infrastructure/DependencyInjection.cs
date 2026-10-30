using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Ports;
using Pos.Application.UseCases;
using Pos.Infrastructure.Odoo;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Security;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Odoo Options
        services.Configure<OdooOptions>(options =>
        {
            var section = configuration.GetSection(OdooOptions.SectionKey);
            section.Bind(options);

            // Support environment variable overrides
            var envBaseUrl = configuration["ODOO__BASEURL"] ?? configuration["ODOO_BASEURL"];
            if (!string.IsNullOrWhiteSpace(envBaseUrl)) options.BaseUrl = envBaseUrl;

            var envDb = configuration["ODOO__DATABASE"] ?? configuration["ODOO_DATABASE"];
            if (!string.IsNullOrWhiteSpace(envDb)) options.Database = envDb;

            var envUser = configuration["ODOO__USERNAME"] ?? configuration["ODOO_USERNAME"];
            if (!string.IsNullOrWhiteSpace(envUser)) options.Username = envUser;

            var envKey = configuration["ODOO__APIKEY"] ?? configuration["ODOO_APIKEY"];
            if (!string.IsNullOrWhiteSpace(envKey)) options.ApiKey = envKey;
        });

        // 2. JWT Options
        services.Configure<JwtOptions>(options =>
        {
            var section = configuration.GetSection(JwtOptions.SectionKey);
            section.Bind(options);

            var envKey = configuration["JWT__SECURITYKEY"] ?? configuration["JWT_SECURITYKEY"];
            if (!string.IsNullOrWhiteSpace(envKey)) options.SecurityKey = envKey;

            var envIssuer = configuration["JWT__VALIDISSUER"] ?? configuration["JWT_VALIDISSUER"];
            if (!string.IsNullOrWhiteSpace(envIssuer)) options.ValidIssuer = envIssuer;

            var envAudience = configuration["JWT__VALIDAUDIENCE"] ?? configuration["JWT_VALIDAUDIENCE"];
            if (!string.IsNullOrWhiteSpace(envAudience)) options.ValidAudience = envAudience;
        });

        // 3. SQLite Local DB (RN-08 Idempotency & Audit)
        var connectionString = configuration.GetConnectionString("SqliteDb")
            ?? configuration["CONNECTIONSTRINGS__SQLITEDB"]
            ?? configuration["CONNECTIONSTRINGS_SQLITEDB"]
            ?? "Data Source=pos_local.db";

        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        // 4. Gateways & Repositories
        services.AddHttpClient<IOdooPosGateway, OdooJson2PosAdapter>((sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<IIdempotencyStore, SqliteIdempotencyStore>();
        services.AddScoped<ISaleHistoryRepository, SqliteSaleHistoryRepository>();
        services.AddScoped<IOperatorRepository, SqliteOperatorRepository>();
        services.AddSingleton<IJwtProvider, JwtService>();

        // 5. Application Use Cases
        services.AddScoped<LoginOperatorUseCase>();
        services.AddScoped<SearchProductsUseCase>();
        services.AddScoped<GetActiveSessionUseCase>();
        services.AddScoped<CreateSaleUseCase>();
        services.AddScoped<GetSalesHistoryUseCase>();
        services.AddScoped<GetSaleByIdUseCase>();

        return services;
    }
}
