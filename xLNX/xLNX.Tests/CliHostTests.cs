namespace xLNX.Tests;

/// <summary>
/// Tests for CLI and Host Lifecycle (SPEC Section 17.7).
/// Tests workflow path resolution behavior.
/// </summary>
[TestClass]
public class CliHostTests
{
    [TestMethod]
    public void CliPathResolution_ExplicitPath_IsUsed()
    {
        // Simulates the logic from Program.cs:
        // var workflowPath = args.Length > 0 && !args[0].StartsWith('-')
        //     ? args[0]
        //     : Path.Combine(Directory.GetCurrentDirectory(), "WORKFLOW.md");
        var args = new[] { "/custom/path/WORKFLOW.md" };
        var workflowPath = args.Length > 0 && !args[0].StartsWith('-')
            ? args[0]
            : Path.Combine(Directory.GetCurrentDirectory(), "WORKFLOW.md");

        Assert.AreEqual("/custom/path/WORKFLOW.md", workflowPath);
    }

    [TestMethod]
    public void CliPathResolution_NoArgs_UsesCwdDefault()
    {
        var args = Array.Empty<string>();
        var workflowPath = args.Length > 0 && !args[0].StartsWith('-')
            ? args[0]
            : Path.Combine(Directory.GetCurrentDirectory(), "WORKFLOW.md");

        Assert.IsTrue(workflowPath.EndsWith("WORKFLOW.md"));
        Assert.IsTrue(Path.IsPathRooted(workflowPath));
    }

    [TestMethod]
    public void CliPathResolution_DashArg_UsesCwdDefault()
    {
        // --port should not be treated as a workflow path
        var args = new[] { "--port", "8080" };
        var workflowPath = args.Length > 0 && !args[0].StartsWith('-')
            ? args[0]
            : Path.Combine(Directory.GetCurrentDirectory(), "WORKFLOW.md");

        Assert.IsTrue(workflowPath.EndsWith("WORKFLOW.md"));
    }

    [TestMethod]
    public void CliPathResolution_NonexistentExplicitPath_WatcherThrows()
    {
        // WorkflowWatcher constructor should throw on nonexistent file
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<xLNX.Core.Workflow.WorkflowWatcher>();
        Assert.ThrowsExactly<FileNotFoundException>(() =>
            new xLNX.Core.Workflow.WorkflowWatcher("/nonexistent/path/WORKFLOW.md", logger));
    }

    [TestMethod]
    public void CliPathResolution_MissingDefaultPath_WorkflowLoaderThrows()
    {
        var ex = Assert.ThrowsExactly<xLNX.Core.Workflow.WorkflowException>(() =>
            xLNX.Core.Workflow.WorkflowLoader.Load("/nonexistent/default/WORKFLOW.md"));
        Assert.AreEqual("missing_workflow_file", ex.ErrorCode);
    }
}
