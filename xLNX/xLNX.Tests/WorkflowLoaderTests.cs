using xLNX.Core.Workflow;

namespace xLNX.Tests;

[TestClass]
public class WorkflowLoaderTests
{
    [TestMethod]
    public void Parse_EmptyContent_ReturnsEmptyConfigAndPrompt()
    {
        var result = WorkflowLoader.Parse("");
        Assert.AreEqual(0, result.Config.Count);
        Assert.AreEqual("", result.PromptTemplate);
    }

    [TestMethod]
    public void Parse_PromptOnly_ReturnsEmptyConfig()
    {
        var result = WorkflowLoader.Parse("Hello {{ issue.title }}");
        Assert.AreEqual(0, result.Config.Count);
        Assert.AreEqual("Hello {{ issue.title }}", result.PromptTemplate);
    }

    [TestMethod]
    public void Parse_FrontMatterAndPrompt_ParsesCorrectly()
    {
        var content = """
            ---
            tracker:
              kind: linear
              project_slug: my-project
            polling:
              interval_ms: 15000
            ---
            Work on {{ issue.identifier }}: {{ issue.title }}
            """;

        var result = WorkflowLoader.Parse(content);
        Assert.IsTrue(result.Config.ContainsKey("tracker"));
        Assert.IsTrue(result.Config.ContainsKey("polling"));
        Assert.IsTrue(result.PromptTemplate.Contains("{{ issue.identifier }}"));
    }

    [TestMethod]
    public void Parse_EmptyFrontMatter_ReturnsEmptyConfig()
    {
        var content = """
            ---
            ---
            Just a prompt.
            """;

        var result = WorkflowLoader.Parse(content);
        Assert.AreEqual(0, result.Config.Count);
        Assert.AreEqual("Just a prompt.", result.PromptTemplate);
    }

    [TestMethod]
    public void Load_MissingFile_ThrowsWorkflowException()
    {
        var ex = Assert.ThrowsExactly<WorkflowException>(() =>
            WorkflowLoader.Load("/nonexistent/WORKFLOW.md"));
        Assert.AreEqual("missing_workflow_file", ex.ErrorCode);
    }

    [TestMethod]
    public void Parse_NonMapFrontMatter_ThrowsWorkflowException()
    {
        var content = """
            ---
            - item1
            - item2
            ---
            Prompt here.
            """;

        var ex = Assert.ThrowsExactly<WorkflowException>(() =>
            WorkflowLoader.Parse(content));
        Assert.AreEqual("workflow_front_matter_not_a_map", ex.ErrorCode);
    }

    [TestMethod]
    public void Parse_ScalarFrontMatter_ThrowsWorkflowException()
    {
        var content = """
            ---
            just a string
            ---
            Prompt here.
            """;

        var ex = Assert.ThrowsExactly<WorkflowException>(() =>
            WorkflowLoader.Parse(content));
        Assert.AreEqual("workflow_front_matter_not_a_map", ex.ErrorCode);
    }

    [TestMethod]
    public void Parse_InvalidYamlFrontMatter_ThrowsWorkflowException()
    {
        var content = """
            ---
            invalid: [yaml: broken
            ---
            Prompt here.
            """;

        var ex = Assert.ThrowsExactly<WorkflowException>(() =>
            WorkflowLoader.Parse(content));
        Assert.AreEqual("workflow_parse_error", ex.ErrorCode);
    }
}
