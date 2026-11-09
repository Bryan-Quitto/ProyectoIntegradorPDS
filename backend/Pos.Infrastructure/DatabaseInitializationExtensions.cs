using System.Data.Common;
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

        // Safe SQLite migrations for evolving columns without throwing duplicate column errors in EF logs
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = false;
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
                shouldClose = true;
            }

            try
            {
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info('SaleAuditRecords');";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        // Index 1 corresponds to the column name in SQLite PRAGMA table_info
                        existingColumns.Add(reader.GetString(1));
                    }
                }

                if (existingColumns.Count > 0)
                {
                    void AddColumnIfMissing(string columnName, string definition)
                    {
                        if (!existingColumns.Contains(columnName))
                        {
#pragma warning disable EF1002
                            dbContext.Database.ExecuteSqlRaw($"ALTER TABLE SaleAuditRecords ADD COLUMN {columnName} {definition};");
#pragma warning restore EF1002
                        }
                    }

                    AddColumnIfMissing("SessionName", "TEXT DEFAULT 'POS/0001'");
                    AddColumnIfMissing("OperatorName", "TEXT DEFAULT 'Cajero POS'");
                    AddColumnIfMissing("DiscountPolicyName", "TEXT DEFAULT 'Sin Descuento'");
                    AddColumnIfMissing("PaymentMethod", "TEXT DEFAULT 'Efectivo'");
                    AddColumnIfMissing("AmountPaid", "TEXT DEFAULT '0.0'");
                }
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }
        catch
        {
            // Database is not SQLite (e.g. In-Memory test database) or schema is already handled
        }
    }
}
