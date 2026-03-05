using Microsoft.Data.SqlClient;
using xLNX.Core.Models;

namespace xLNX.Data.Repositories;

/// <summary>
/// Repository for persisting and retrieving run attempt data.
/// </summary>
public class RunAttemptRepository
{
    private readonly string _connectionString;

    public RunAttemptRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Saves a run attempt record.
    /// </summary>
    public async Task<int> SaveAsync(RunAttempt attempt, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO RunAttempts (IssueId, IssueIdentifier, Attempt, WorkspacePath, StartedAt, Status, Error)
            OUTPUT INSERTED.Id
            VALUES (@IssueId, @IssueIdentifier, @Attempt, @WorkspacePath, @StartedAt, @Status, @Error)
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IssueId", attempt.IssueId);
        cmd.Parameters.AddWithValue("@IssueIdentifier", attempt.IssueIdentifier);
        cmd.Parameters.AddWithValue("@Attempt", (object?)attempt.Attempt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@WorkspacePath", attempt.WorkspacePath);
        cmd.Parameters.AddWithValue("@StartedAt", attempt.StartedAt);
        cmd.Parameters.AddWithValue("@Status", attempt.Status.ToString());
        cmd.Parameters.AddWithValue("@Error", (object?)attempt.Error ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Updates the status and completion of a run attempt.
    /// </summary>
    public async Task UpdateStatusAsync(int id, RunAttemptStatus status, string? error, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE RunAttempts
            SET Status = @Status, Error = @Error, CompletedAt = GETUTCDATE()
            WHERE Id = @Id
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Status", status.ToString());
        cmd.Parameters.AddWithValue("@Error", (object?)error ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Gets recent run attempts for an issue.
    /// </summary>
    public async Task<List<RunAttempt>> GetByIssueIdAsync(string issueId, int limit = 10, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP(@Limit) IssueId, IssueIdentifier, Attempt, WorkspacePath, StartedAt, Status, Error
            FROM RunAttempts
            WHERE IssueId = @IssueId
            ORDER BY StartedAt DESC
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@IssueId", issueId);
        cmd.Parameters.AddWithValue("@Limit", limit);

        var results = new List<RunAttempt>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new RunAttempt
            {
                IssueId = reader.GetString(0),
                IssueIdentifier = reader.GetString(1),
                Attempt = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                WorkspacePath = reader.GetString(3),
                StartedAt = reader.GetDateTime(4),
                Status = Enum.Parse<RunAttemptStatus>(reader.GetString(5)),
                Error = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return results;
    }
}
