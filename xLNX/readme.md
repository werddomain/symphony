# Symphony xLNX

A .NET 10 implementation of the [Symphony SPEC](../SPEC.md) — a long-running automation service that orchestrates coding agents to execute project work sourced from an issue tracker.

## Solution Structure

| Project | Description |
|---------|-------------|
| **xLNX.Core** | Domain models, orchestration engine, workflow parsing, configuration, agent runner, tracker integration |
| **xLNX.Web** | ASP.NET Core Blazor dashboard + REST API for monitoring and managing the orchestrator |
| **xLNX.Runner** | WinForms desktop runner (Windows only) |
| **xLNX.Data** | Data access layer |
| **xLNX.Tests** | MSTest v4 unit/integration tests for Core |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A `WORKFLOW.md` file (see [SPEC §5](../SPEC.md)) in the working directory or passed as a CLI argument

### Build

```bash
cd xLNX
dotnet build xLNX.slnx
```

### Run Tests

```bash
cd xLNX
dotnet test xLNX.Tests/xLNX.Tests.csproj
```

### Run the Web Dashboard

```bash
cd xLNX/xLNX.Web
dotnet run
# Or with a custom workflow file:
dotnet run -- /path/to/WORKFLOW.md
```

The dashboard is available at `https://localhost:5001` (or the configured port).

## Web Dashboard

The web project (`xLNX.Web`) provides a Blazor Server dashboard for monitoring and managing the Symphony orchestrator. See [xLNX.Web/plan.md](xLNX.Web/plan.md) for the full feature plan.

### Pages

| Page | Route | Description |
|------|-------|-------------|
| Dashboard | `/` | System overview with running/retrying counts, token totals, runtime stats |
| Sessions | `/sessions` | Live table of all active coding-agent sessions |
| Retries | `/retries` | Retry queue with backoff timing and error reasons |
| Issue Detail | `/issues/{id}` | Per-issue view with full metadata, session info, and actions |
| Configuration | `/configuration` | Current effective service configuration from WORKFLOW.md |
| Workflow | `/workflow` | Parsed workflow definition: YAML config and prompt template |

### REST API

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/state` | GET | Current system state summary (running, retrying, totals) |
| `/api/v1/issues/{identifier}` | GET | Issue-specific runtime details |
| `/api/v1/refresh` | POST | Trigger immediate poll and reconciliation |

## Architecture

```
WORKFLOW.md ──► WorkflowWatcher ──► ServiceConfig + WorkflowDefinition
                                         │
                    ┌────────────────────┘
                    ▼
              Orchestrator ◄──── OrchestratorHostedService (BackgroundService)
                    │
          ┌─────────┼──────────┐
          ▼         ▼          ▼
     LinearClient  AgentRunner  WorkspaceManager
     (tracker)     (codex)      (filesystem)
```

The orchestrator runs as a hosted background service, polling the issue tracker at a configurable interval, dispatching eligible issues to coding agents, managing retries with exponential backoff, and reconciling state changes.

## Key Concepts

- **WorkflowWatcher**: Monitors `WORKFLOW.md` for changes and hot-reloads configuration without restart
- **Orchestrator**: Core state machine managing the poll → dispatch → monitor → retry loop
- **AgentRunner**: Manages codex app-server subprocess lifecycle and event streaming
- **WorkspaceManager**: Creates and manages per-issue filesystem workspaces
- **LinearClient**: Integrates with Linear issue tracker via GraphQL API

## Configuration

All configuration is defined in the YAML front-matter of `WORKFLOW.md`. See [SPEC §6](../SPEC.md) for the full configuration reference.

## License

See [LICENSE](../LICENSE) in the repository root.
