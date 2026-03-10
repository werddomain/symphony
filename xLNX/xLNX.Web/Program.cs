using xLNX.Core.Agent;
using xLNX.Core.Models;
using xLNX.Core.Orchestration;
using xLNX.Core.Tracker;
using xLNX.Core.Workflow;
using xLNX.Core.Workspaces;
using xLNX.Web;
using xLNX.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddRazorComponents();

// Determine workflow path: CLI arg or default WORKFLOW.md
var workflowPath = args.Length > 0 && !args[0].StartsWith('-')
    ? args[0]
    : Path.Combine(Directory.GetCurrentDirectory(), "WORKFLOW.md");

// Register workflow watcher as singleton (SPEC 6.2)
builder.Services.AddSingleton(sp =>
    new WorkflowWatcher(workflowPath, sp.GetRequiredService<ILogger<WorkflowWatcher>>()));

// Config and workflow providers from watcher
builder.Services.AddSingleton<Func<ServiceConfig>>(sp =>
    () => sp.GetRequiredService<WorkflowWatcher>().CurrentConfig);
builder.Services.AddSingleton<Func<WorkflowDefinition>>(sp =>
    () => sp.GetRequiredService<WorkflowWatcher>().CurrentWorkflow);

// Core services
builder.Services.AddSingleton<WorkspaceManager>();
builder.Services.AddHttpClient<LinearClient>();
builder.Services.AddSingleton<IIssueTrackerClient>(sp => sp.GetRequiredService<LinearClient>());
builder.Services.AddSingleton<AgentRunner>();

// Orchestrator (SPEC 16.1)
builder.Services.AddSingleton<Orchestrator>();
builder.Services.AddSingleton<Func<OrchestratorState>>(sp =>
    () => sp.GetRequiredService<Orchestrator>().State);

// Background service to run the orchestrator loop
builder.Services.AddHostedService<OrchestratorHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();
app.MapControllers();
app.MapRazorComponents<App>();

app.Run();
