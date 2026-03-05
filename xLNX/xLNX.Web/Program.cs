using xLNX.Core.Models;
using xLNX.Core.Orchestration;
using xLNX.Web.Controllers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Register orchestrator state as a singleton (to be provided by the host)
builder.Services.AddSingleton<Func<OrchestratorState>>(_ => () => new OrchestratorState());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.MapControllers();

// Dashboard at /
app.MapGet("/", () => Results.Content(DashboardHtml.Render(), "text/html"));

app.Run();
