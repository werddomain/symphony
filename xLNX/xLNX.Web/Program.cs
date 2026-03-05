using xLNX.Core.Models;
using xLNX.Core.Orchestration;
using xLNX.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddRazorComponents();

// Register orchestrator state as a singleton (to be provided by the host)
builder.Services.AddSingleton<Func<OrchestratorState>>(_ => () => new OrchestratorState());

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
