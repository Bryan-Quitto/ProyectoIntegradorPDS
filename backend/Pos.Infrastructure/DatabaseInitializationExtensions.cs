using System.Data;
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
        dbContext.EnsurePosSchemaAndFtsInitialized();
    }

    /// <summary>
    /// Inicializa de forma idempotente el esquema, columnas evolutivas, índices B-Tree,
    /// tabla virtual FTS5 (trigram) y triggers de sincronización para SQLite.
    /// Ejecuta el rebuild inicial de FTS5 únicamente cuando SaleAuditRecords contiene filas
    /// y el índice FTS5 está vacío.
    /// </summary>
    public static void EnsurePosSchemaAndFtsInitialized(this PosDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        // Safe SQLite migrations for evolving columns without throwing duplicate column errors in EF logs
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = false;
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
            shouldClose = true;
        }

        try
        {
            EnsureMissingColumns(dbContext, connection);
            EnsureBTreeIndexes(connection);
            EnsureFts5SchemaAndTriggers(connection);
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static void EnsureMissingColumns(PosDbContext dbContext, DbConnection connection)
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

        if (existingColumns.Count == 0)
        {
            return;
        }

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
        AddColumnIfMissing("ChangeAmount", "TEXT DEFAULT '0.0'");
    }

    private static void EnsureBTreeIndexes(DbConnection connection)
    {
        ExecuteNonQuery(connection, """
            CREATE INDEX IF NOT EXISTS "IX_SaleAuditRecords_SyncedAtUtc"
                ON "SaleAuditRecords" ("SyncedAtUtc" DESC);
            CREATE INDEX IF NOT EXISTS "IX_SaleAuditRecords_PosSessionId_SyncedAtUtc"
                ON "SaleAuditRecords" ("PosSessionId", "SyncedAtUtc" DESC);
            CREATE INDEX IF NOT EXISTS "IX_SaleAuditRecords_CustomerDocument"
                ON "SaleAuditRecords" ("CustomerDocument");
            """);
    }

    private static void EnsureFts5SchemaAndTriggers(DbConnection connection)
    {
        ExecuteNonQuery(connection, """
            CREATE VIRTUAL TABLE IF NOT EXISTS "SaleAuditRecordsFts" USING fts5(
                "OrderId",
                "OdooOrderReference",
                "CustomerName",
                "CustomerDocument",
                "SessionName",
                content='SaleAuditRecords',
                content_rowid='Id',
                tokenize='trigram'
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TRIGGER IF NOT EXISTS "SaleAuditRecords_ai" AFTER INSERT ON "SaleAuditRecords" BEGIN
                INSERT INTO "SaleAuditRecordsFts"(rowid, OrderId, OdooOrderReference, CustomerName, CustomerDocument, SessionName)
                VALUES (new.Id, new.OrderId, new.OdooOrderReference, new.CustomerName, new.CustomerDocument, new.SessionName);
            END;
            """);

        ExecuteNonQuery(connection, """
            CREATE TRIGGER IF NOT EXISTS "SaleAuditRecords_ad" AFTER DELETE ON "SaleAuditRecords" BEGIN
                INSERT INTO "SaleAuditRecordsFts"(SaleAuditRecordsFts, rowid, OrderId, OdooOrderReference, CustomerName, CustomerDocument, SessionName)
                VALUES ('delete', old.Id, old.OrderId, old.OdooOrderReference, old.CustomerName, old.CustomerDocument, old.SessionName);
            END;
            """);

        ExecuteNonQuery(connection, """
            CREATE TRIGGER IF NOT EXISTS "SaleAuditRecords_au" AFTER UPDATE ON "SaleAuditRecords" BEGIN
                INSERT INTO "SaleAuditRecordsFts"(SaleAuditRecordsFts, rowid, OrderId, OdooOrderReference, CustomerName, CustomerDocument, SessionName)
                VALUES ('delete', old.Id, old.OrderId, old.OdooOrderReference, old.CustomerName, old.CustomerDocument, old.SessionName);
                INSERT INTO "SaleAuditRecordsFts"(rowid, OrderId, OdooOrderReference, CustomerName, CustomerDocument, SessionName)
                VALUES (new.Id, new.OrderId, new.OdooOrderReference, new.CustomerName, new.CustomerDocument, new.SessionName);
            END;
            """);

        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = """
            SELECT CASE
                WHEN EXISTS(SELECT 1 FROM "SaleAuditRecords")
                 AND NOT EXISTS(SELECT 1 FROM "SaleAuditRecordsFts_docsize")
                THEN 1 ELSE 0
            END;
            """;
        var needsRebuild = Convert.ToInt64(checkCmd.ExecuteScalar() ?? 0L) == 1L;
        if (needsRebuild)
        {
            ExecuteNonQuery(connection, """INSERT INTO "SaleAuditRecordsFts"("SaleAuditRecordsFts") VALUES('rebuild');""");
        }
    }

    private static void ExecuteNonQuery(DbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
