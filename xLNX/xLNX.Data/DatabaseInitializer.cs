using Microsoft.Data.SqlClient;

namespace xLNX.Data;

/// <summary>
/// Manages database initialization and migration.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Ensures the database schema is created.
    /// </summary>
    public static async Task InitializeAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(Schema.DatabaseSchema.CreateTables, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
