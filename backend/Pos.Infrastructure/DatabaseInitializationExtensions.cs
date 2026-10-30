using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure;

public static class DatabaseInitializationExtensions
{
    public static void InitializePosDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        dbContext.Database.EnsureCreated();

        // Safe SQLite migrations for evolving columns
        try
        {
#pragma warning disable EF1002
            dbContext.Database.ExecuteSqlRaw("ALTER TABLE SaleAuditRecords ADD COLUMN SessionName TEXT DEFAULT 'POS/0001';");
            dbContext.Database.ExecuteSqlRaw("ALTER TABLE SaleAuditRecords ADD COLUMN OperatorName TEXT DEFAULT 'Cajero POS';");
            dbContext.Database.ExecuteSqlRaw("ALTER TABLE SaleAuditRecords ADD COLUMN DiscountPolicyName TEXT DEFAULT 'Sin Descuento';");
            dbContext.Database.ExecuteSqlRaw("ALTER TABLE SaleAuditRecords ADD COLUMN PaymentMethod TEXT DEFAULT 'Efectivo';");
            dbContext.Database.ExecuteSqlRaw("ALTER TABLE SaleAuditRecords ADD COLUMN AmountPaid TEXT DEFAULT '0.0';");
#pragma warning restore EF1002
        }
        catch
        {
            // Columns already exist or database not SQLite
        }
    }
}
