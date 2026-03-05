namespace xLNX.Data.Schema;

/// <summary>
/// SQL schema definitions for Symphony xLNX persistent storage.
/// Contains the DDL statements for MS SQL Server.
/// </summary>
public static class DatabaseSchema
{
    /// <summary>
    /// Creates all tables needed for Symphony xLNX persistent state.
    /// </summary>
    public const string CreateTables = """
        -- Issue tracking state persistence
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Issues')
        CREATE TABLE Issues (
            Id NVARCHAR(100) NOT NULL PRIMARY KEY,
            Identifier NVARCHAR(100) NOT NULL,
            Title NVARCHAR(500) NOT NULL,
            Description NVARCHAR(MAX) NULL,
            Priority INT NULL,
            State NVARCHAR(100) NOT NULL,
            BranchName NVARCHAR(200) NULL,
            Url NVARCHAR(500) NULL,
            CreatedAt DATETIME2 NULL,
            UpdatedAt DATETIME2 NULL,
            LastSyncedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
        );

        -- Run attempt history
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RunAttempts')
        CREATE TABLE RunAttempts (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            IssueId NVARCHAR(100) NOT NULL,
            IssueIdentifier NVARCHAR(100) NOT NULL,
            Attempt INT NULL,
            WorkspacePath NVARCHAR(500) NOT NULL,
            StartedAt DATETIME2 NOT NULL,
            CompletedAt DATETIME2 NULL,
            Status NVARCHAR(50) NOT NULL,
            Error NVARCHAR(MAX) NULL,
            CONSTRAINT FK_RunAttempts_Issues FOREIGN KEY (IssueId) REFERENCES Issues(Id)
        );

        -- Session metrics and token accounting
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SessionMetrics')
        CREATE TABLE SessionMetrics (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            RunAttemptId INT NOT NULL,
            SessionId NVARCHAR(200) NOT NULL,
            ThreadId NVARCHAR(100) NOT NULL,
            TurnId NVARCHAR(100) NOT NULL,
            TurnCount INT NOT NULL DEFAULT 0,
            InputTokens BIGINT NOT NULL DEFAULT 0,
            OutputTokens BIGINT NOT NULL DEFAULT 0,
            TotalTokens BIGINT NOT NULL DEFAULT 0,
            SecondsRunning FLOAT NOT NULL DEFAULT 0,
            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT FK_SessionMetrics_RunAttempts FOREIGN KEY (RunAttemptId) REFERENCES RunAttempts(Id)
        );

        -- Retry queue persistence
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RetryQueue')
        CREATE TABLE RetryQueue (
            IssueId NVARCHAR(100) NOT NULL PRIMARY KEY,
            Identifier NVARCHAR(100) NOT NULL,
            Attempt INT NOT NULL,
            DueAtMs BIGINT NOT NULL,
            Error NVARCHAR(MAX) NULL,
            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT FK_RetryQueue_Issues FOREIGN KEY (IssueId) REFERENCES Issues(Id)
        );

        -- Configuration history
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ConfigSnapshots')
        CREATE TABLE ConfigSnapshots (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            ConfigJson NVARCHAR(MAX) NOT NULL,
            LoadedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            Source NVARCHAR(200) NOT NULL
        );

        -- Indexes for performance
        CREATE NONCLUSTERED INDEX IX_Issues_State ON Issues(State) WHERE State IS NOT NULL;
        CREATE NONCLUSTERED INDEX IX_RunAttempts_IssueId ON RunAttempts(IssueId);
        CREATE NONCLUSTERED INDEX IX_RunAttempts_Status ON RunAttempts(Status);
        CREATE NONCLUSTERED INDEX IX_SessionMetrics_RunAttemptId ON SessionMetrics(RunAttemptId);
        """;
}
