using xLNX.Core.Models;
using xLNX.Core.Workflow;

namespace xLNX.Tests;

[TestClass]
public class PromptRendererTests
{
    [TestMethod]
    public void Render_EmptyTemplate_ReturnsDefaultPrompt()
    {
        var issue = new Issue { Id = "1", Identifier = "TEST-1", Title = "Test" };
        var result = PromptRenderer.Render("", issue, null);
        Assert.AreEqual("You are working on an issue from Linear.", result);
    }

    [TestMethod]
    public void Render_SimpleTemplate_RendersIssueFields()
    {
        var issue = new Issue
        {
            Id = "1",
            Identifier = "TEST-1",
            Title = "Fix the bug",
            State = "Todo"
        };

        var result = PromptRenderer.Render(
            "Work on {{ issue.Identifier }}: {{ issue.Title }}",
            issue, null);

        Assert.AreEqual("Work on TEST-1: Fix the bug", result);
    }

    [TestMethod]
    public void Render_WithAttempt_RendersAttemptNumber()
    {
        var issue = new Issue { Id = "1", Identifier = "TEST-1", Title = "Test" };
        var result = PromptRenderer.Render(
            "Attempt {{ attempt }}",
            issue, 3);

        Assert.AreEqual("Attempt 3", result);
    }

    [TestMethod]
    public void Render_InvalidTemplate_ThrowsWorkflowException()
    {
        var issue = new Issue { Id = "1", Identifier = "TEST-1", Title = "Test" };
        Assert.ThrowsExactly<WorkflowException>(() =>
            PromptRenderer.Render("{% invalid %}", issue, null));
    }
}
