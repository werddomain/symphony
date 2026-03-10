using xLNX.Core.Workflow;

namespace xLNX.Tests;

[TestClass]
public class WorkflowWatcherTests
{
    [TestMethod]
    public void WorkflowWatcher_LoadsInitialWorkflow()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                ---
                tracker:
                  kind: linear
                  api_key: test-key
                  project_slug: my-proj
                ---
                Hello {{ issue.identifier }}
                """);

            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowWatcher>();
            using var watcher = new WorkflowWatcher(tempFile, logger);

            Assert.AreEqual("linear", watcher.CurrentConfig.TrackerKind);
            Assert.AreEqual("my-proj", watcher.CurrentConfig.TrackerProjectSlug);
            Assert.IsTrue(watcher.CurrentWorkflow.PromptTemplate.Contains("Hello"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void WorkflowWatcher_ReloadUpdatesConfig()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                ---
                tracker:
                  kind: linear
                  api_key: test-key
                  project_slug: proj-1
                ---
                Prompt v1
                """);

            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowWatcher>();
            using var watcher = new WorkflowWatcher(tempFile, logger);

            Assert.AreEqual("proj-1", watcher.CurrentConfig.TrackerProjectSlug);

            // Update file
            File.WriteAllText(tempFile, """
                ---
                tracker:
                  kind: linear
                  api_key: test-key
                  project_slug: proj-2
                ---
                Prompt v2
                """);

            watcher.Reload();

            Assert.AreEqual("proj-2", watcher.CurrentConfig.TrackerProjectSlug);
            Assert.IsTrue(watcher.CurrentWorkflow.PromptTemplate.Contains("Prompt v2"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void WorkflowWatcher_InvalidReloadKeepsLastGoodConfig()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                ---
                tracker:
                  kind: linear
                  api_key: test-key
                  project_slug: good-proj
                ---
                Good prompt
                """);

            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowWatcher>();
            using var watcher = new WorkflowWatcher(tempFile, logger);

            Assert.AreEqual("good-proj", watcher.CurrentConfig.TrackerProjectSlug);

            // Delete file to make reload fail
            File.Delete(tempFile);
            watcher.Reload();

            // Should keep last good config
            Assert.AreEqual("good-proj", watcher.CurrentConfig.TrackerProjectSlug);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
