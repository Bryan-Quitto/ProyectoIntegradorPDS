using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pos.Infrastructure.Persistence;

/// <summary>
/// Interceptor de conexión SQLite que configura PRAGMAs moderados para balancear
/// rendimiento de lectura (caché 24 MB, mmap 256 MB, temp_store en RAM) sin saturar
/// memoria bajo el pool de EF Core.
/// </summary>
public sealed class SqlitePragmaConnectionInterceptor : DbConnectionInterceptor
{
    public static readonly SqlitePragmaConnectionInterceptor Instance = new();

    private const string PragmaSql = """
        PRAGMA cache_size = -24576;
        PRAGMA mmap_size = 268435456;
        PRAGMA temp_store = MEMORY;
        PRAGMA synchronous = NORMAL;
        """;

    private SqlitePragmaConnectionInterceptor()
    {
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyPragmasAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = PragmaSql;
        command.ExecuteNonQuery();
    }

    private static async Task ApplyPragmasAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = PragmaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
