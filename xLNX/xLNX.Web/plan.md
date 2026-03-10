# Symphony xLNX Web Dashboard — Implementation Plan

This document tracks the planned features for the xLNX.Web project, derived from [SPEC.md](../../SPEC.md).

## Pages & Components

| # | Page / Component | Route | Description | Status |
|---|-----------------|-------|-------------|--------|
| 1 | **Dashboard** | `/` | System overview: running/retrying counts, token totals, runtime, rate-limits, quick stats | Completed |
| 2 | **Sessions** | `/sessions` | Live table of all active agent sessions with issue, state, turns, tokens, elapsed time | Completed |
| 3 | **Retries** | `/retries` | Retry queue with attempt number, backoff due time, error reason | Completed |
| 4 | **Issue Detail** | `/issues/{identifier}` | Per-issue view: issue metadata, running session or retry info, tokens, actions | Completed |
| 5 | **Configuration** | `/configuration` | Displays current effective `ServiceConfig` from WORKFLOW.md front-matter | Completed |
| 6 | **Workflow** | `/workflow` | Shows parsed workflow definition: YAML config + prompt template | Completed |
| 7 | **MainLayout** | _(layout)_ | Sidebar navigation + top bar with status indicator | Completed |
| 8 | **App Shell** | _(root)_ | HTML head, CSS/JS references, HeadOutlet, Routes | Completed |

## Code-Behind Convention

Every page uses the separated file pattern:

- `PageName.razor` — Markup only
- `PageName.razor.cs` — Code-behind (partial class with `@code` logic)
- `PageName.razor.css` — Scoped CSS styles
- `PageName.razor.js` — Scoped JavaScript (loaded via `<script>` or JS interop)

## SPEC Feature Coverage

### Core Domain (SPEC §4)

| Feature | Surfaced In | Status |
|---------|------------|--------|
| Issue entity display (id, identifier, title, state, priority, labels, blockers) | Issue Detail, Sessions | Completed |
| LiveSession display (session_id, turns, last_event, tokens) | Dashboard, Sessions, Issue Detail | Completed |
| RunningEntry display (issue + session + started_at) | Sessions, Dashboard | Completed |
| RetryEntry display (attempt, due_at, error) | Retries, Dashboard | Completed |
| OrchestratorState summary (counts, totals, rate-limits) | Dashboard | Completed |
| CodexTotals display (input/output/total tokens, seconds_running) | Dashboard | Completed |
| ServiceConfig viewer (all config sections) | Configuration | Completed |
| WorkflowDefinition viewer (YAML + prompt) | Workflow | Completed |

### Orchestration & Monitoring (SPEC §7, §8, §13)

| Feature | Surfaced In | Status |
|---------|------------|--------|
| Running session count | Dashboard | Completed |
| Retrying count | Dashboard | Completed |
| Token accounting (live aggregate) | Dashboard, Sessions | Completed |
| Runtime seconds (live) | Dashboard | Completed |
| Rate-limit snapshot | Dashboard | Completed |
| Trigger manual poll (POST /api/v1/refresh) | Dashboard, Issue Detail | Completed |
| Per-issue detail (GET /api/v1/issues/{identifier}) | Issue Detail | Completed |
| Auto-refresh polling (periodic data fetch) | All pages | Completed |

### Configuration (SPEC §6)

| Feature | Surfaced In | Status |
|---------|------------|--------|
| Tracker settings display | Configuration | Completed |
| Polling interval display | Configuration | Completed |
| Workspace root display | Configuration | Completed |
| Hook configurations display | Configuration | Completed |
| Agent settings display (concurrency, turns, backoff) | Configuration | Completed |
| Codex settings display (command, policies, timeouts) | Configuration | Completed |
| Dynamic reload indication | Configuration | Completed |

### Workflow (SPEC §5)

| Feature | Surfaced In | Status |
|---------|------------|--------|
| Prompt template display | Workflow | Completed |
| YAML front-matter display | Workflow | Completed |
| Workflow file path display | Workflow | Completed |

## API Endpoints Consumed

| Endpoint | Method | Used By |
|----------|--------|---------|
| `/api/v1/state` | GET | Dashboard, Sessions, Retries |
| `/api/v1/issues/{identifier}` | GET | Issue Detail |
| `/api/v1/refresh` | POST | Dashboard, Issue Detail |

## Future Enhancements (Not Yet Planned)

| Feature | Description | Status |
|---------|-------------|--------|
| Metrics charts | Token usage / runtime over time with charting library | Pending |
| Log viewer | Structured log search and filtering | Pending |
| Health diagnostics | Service status, uptime, error indicators | Pending |
| Workspace browser | View workspace filesystem contents | Pending |
| Session cancel action | Stop a running agent session | Pending |
| WORKFLOW.md editor | In-browser editing with validation | Pending |
