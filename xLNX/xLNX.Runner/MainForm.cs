using xLNX.Core.Configuration;
using xLNX.Core.Models;
using xLNX.Core.Workflow;

namespace xLNX.Runner;

/// <summary>
/// Main WinForms application window hosting TCP Socket, MCP Server,
/// Configuration UI, logs and stats.
/// </summary>
public partial class MainForm : Form
{
    private ServiceConfig _config = new();
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public MainForm()
    {
        InitializeComponent();
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LoadConfiguration();
        _refreshTimer.Start();
        AppendLog("Symphony xLNX Runner started.");
    }

    private void LoadConfiguration()
    {
        try
        {
            var workflowPath = Path.Combine(Directory.GetCurrentDirectory(), "WORKFLOW.md");
            if (File.Exists(workflowPath))
            {
                var workflow = WorkflowLoader.Load(workflowPath);
                _config = ConfigLayer.Build(workflow.Config);
                UpdateConfigDisplay();
                AppendLog("Configuration loaded from WORKFLOW.md");
            }
            else
            {
                AppendLog("No WORKFLOW.md found, using defaults.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error loading config: {ex.Message}");
        }
    }

    private void UpdateConfigDisplay()
    {
        if (txtTrackerKind != null) txtTrackerKind.Text = _config.TrackerKind;
        if (txtProjectSlug != null) txtProjectSlug.Text = _config.TrackerProjectSlug;
        if (numPollInterval != null) numPollInterval.Value = _config.PollIntervalMs;
        if (numMaxAgents != null) numMaxAgents.Value = _config.MaxConcurrentAgents;
        if (txtWorkspaceRoot != null) txtWorkspaceRoot.Text = _config.WorkspaceRoot;
        if (numTurnTimeout != null) numTurnTimeout.Value = _config.CodexTurnTimeoutMs;
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{timestamp}] {message}";
        if (txtLogs != null && txtLogs.IsHandleCreated)
        {
            if (txtLogs.InvokeRequired)
            {
                txtLogs.Invoke(() => txtLogs.AppendText(line + Environment.NewLine));
            }
            else
            {
                txtLogs.AppendText(line + Environment.NewLine);
            }
        }
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        // Refresh stats display
        if (lblRunningCount != null) lblRunningCount.Text = "0";
        if (lblRetryCount != null) lblRetryCount.Text = "0";
        if (lblTotalTokens != null) lblTotalTokens.Text = "0";
    }

    private void BtnReloadConfig_Click(object? sender, EventArgs e)
    {
        LoadConfiguration();
    }

    private void BtnStartService_Click(object? sender, EventArgs e)
    {
        AppendLog("Service start requested...");
    }

    private void BtnStopService_Click(object? sender, EventArgs e)
    {
        AppendLog("Service stop requested...");
    }
}
