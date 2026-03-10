namespace xLNX.Runner;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    // Configuration tab controls
    private TextBox txtTrackerKind = null!;
    private TextBox txtProjectSlug = null!;
    private NumericUpDown numPollInterval = null!;
    private NumericUpDown numMaxAgents = null!;
    private TextBox txtWorkspaceRoot = null!;
    private NumericUpDown numTurnTimeout = null!;

    // Logs
    private TextBox txtLogs = null!;

    // Stats
    private Label lblRunningCount = null!;
    private Label lblRetryCount = null!;
    private Label lblTotalTokens = null!;

    // Buttons
    private Button btnReloadConfig = null!;
    private Button btnStartService = null!;
    private Button btnStopService = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        // Main form
        Text = "Symphony xLNX Runner";
        Size = new Size(900, 650);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);

        var tabControl = new TabControl { Dock = DockStyle.Fill };

        // === Configuration Tab ===
        var tabConfig = new TabPage("Configuration");
        var configPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(10)
        };
        configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        configPanel.Controls.Add(new Label { Text = "Tracker Kind:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        txtTrackerKind = new TextBox { Dock = DockStyle.Fill };
        configPanel.Controls.Add(txtTrackerKind, 1, row++);

        configPanel.Controls.Add(new Label { Text = "Project Slug:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        txtProjectSlug = new TextBox { Dock = DockStyle.Fill };
        configPanel.Controls.Add(txtProjectSlug, 1, row++);

        configPanel.Controls.Add(new Label { Text = "Poll Interval (ms):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        numPollInterval = new NumericUpDown { Dock = DockStyle.Fill, Maximum = 600_000, Minimum = 1_000, Value = 30_000 };
        configPanel.Controls.Add(numPollInterval, 1, row++);

        configPanel.Controls.Add(new Label { Text = "Max Agents:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        numMaxAgents = new NumericUpDown { Dock = DockStyle.Fill, Maximum = 100, Minimum = 1, Value = 10 };
        configPanel.Controls.Add(numMaxAgents, 1, row++);

        configPanel.Controls.Add(new Label { Text = "Workspace Root:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        txtWorkspaceRoot = new TextBox { Dock = DockStyle.Fill };
        configPanel.Controls.Add(txtWorkspaceRoot, 1, row++);

        configPanel.Controls.Add(new Label { Text = "Turn Timeout (ms):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        numTurnTimeout = new NumericUpDown { Dock = DockStyle.Fill, Maximum = 7_200_000, Minimum = 60_000, Value = 3_600_000 };
        configPanel.Controls.Add(numTurnTimeout, 1, row++);

        // Buttons
        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        btnReloadConfig = new Button { Text = "Reload Config", AutoSize = true };
        btnReloadConfig.Click += BtnReloadConfig_Click;
        btnStartService = new Button { Text = "Start Service", AutoSize = true };
        btnStartService.Click += BtnStartService_Click;
        btnStopService = new Button { Text = "Stop Service", AutoSize = true };
        btnStopService.Click += BtnStopService_Click;
        btnPanel.Controls.AddRange([btnReloadConfig, btnStartService, btnStopService]);
        configPanel.Controls.Add(btnPanel, 0, row);
        configPanel.SetColumnSpan(btnPanel, 2);

        tabConfig.Controls.Add(configPanel);

        // === Logs Tab ===
        var tabLogs = new TabPage("Logs");
        txtLogs = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f)
        };
        tabLogs.Controls.Add(txtLogs);

        // === Stats Tab ===
        var tabStats = new TabPage("Stats");
        var statsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10)
        };
        statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        statsPanel.Controls.Add(new Label { Text = "Running:", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        lblRunningCount = new Label { Text = "0", Anchor = AnchorStyles.Left, AutoSize = true };
        statsPanel.Controls.Add(lblRunningCount, 1, 0);

        statsPanel.Controls.Add(new Label { Text = "Retrying:", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 1);
        lblRetryCount = new Label { Text = "0", Anchor = AnchorStyles.Left, AutoSize = true };
        statsPanel.Controls.Add(lblRetryCount, 1, 1);

        statsPanel.Controls.Add(new Label { Text = "Total Tokens:", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 2);
        lblTotalTokens = new Label { Text = "0", Anchor = AnchorStyles.Left, AutoSize = true };
        statsPanel.Controls.Add(lblTotalTokens, 1, 2);

        tabStats.Controls.Add(statsPanel);

        // Add tabs
        tabControl.TabPages.AddRange([tabConfig, tabLogs, tabStats]);
        Controls.Add(tabControl);
    }
}
