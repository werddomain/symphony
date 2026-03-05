using xLNX.Core.Configuration;
using xLNX.Core.Models;

namespace xLNX.Tests;

[TestClass]
public class ConfigLayerTests
{
    [TestMethod]
    public void Build_EmptyConfig_ReturnsDefaults()
    {
        var config = ConfigLayer.Build(new Dictionary<string, object?>());

        Assert.AreEqual(string.Empty, config.TrackerKind);
        Assert.AreEqual(30_000, config.PollIntervalMs);
        Assert.AreEqual(10, config.MaxConcurrentAgents);
        Assert.AreEqual(20, config.MaxTurns);
        Assert.AreEqual(300_000, config.MaxRetryBackoffMs);
        Assert.AreEqual("codex app-server", config.CodexCommand);
        Assert.AreEqual(3_600_000, config.CodexTurnTimeoutMs);
        Assert.AreEqual(5_000, config.CodexReadTimeoutMs);
        Assert.AreEqual(300_000, config.CodexStallTimeoutMs);
        Assert.AreEqual(60_000, config.HookTimeoutMs);
    }

    [TestMethod]
    public void Validate_MissingTrackerKind_ReturnsError()
    {
        var config = new ServiceConfig();
        var result = ConfigLayer.Validate(config);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("tracker.kind")));
    }

    [TestMethod]
    public void Validate_ValidLinearConfig_Passes()
    {
        var config = new ServiceConfig
        {
            TrackerKind = "linear",
            TrackerApiKey = "test-key",
            TrackerProjectSlug = "my-project",
            CodexCommand = "codex app-server"
        };

        var result = ConfigLayer.Validate(config);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void Validate_UnsupportedTrackerKind_ReturnsError()
    {
        var config = new ServiceConfig
        {
            TrackerKind = "jira",
            TrackerApiKey = "key",
            TrackerProjectSlug = "slug"
        };

        var result = ConfigLayer.Validate(config);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Unsupported")));
    }

    [TestMethod]
    public void Validate_MissingApiKey_ReturnsError()
    {
        var config = new ServiceConfig
        {
            TrackerKind = "linear",
            TrackerProjectSlug = "slug"
        };

        var result = ConfigLayer.Validate(config);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("api_key")));
    }

    [TestMethod]
    public void Validate_MissingProjectSlug_ReturnsError()
    {
        var config = new ServiceConfig
        {
            TrackerKind = "linear",
            TrackerApiKey = "key"
        };

        var result = ConfigLayer.Validate(config);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("project_slug")));
    }

    [TestMethod]
    public void ResolveEnvValue_LiteralValue_ReturnsSame()
    {
        Assert.AreEqual("literal", ConfigLayer.ResolveEnvValue("literal"));
    }

    [TestMethod]
    public void ResolveEnvValue_EnvRef_ResolvesFromEnvironment()
    {
        Environment.SetEnvironmentVariable("XLNX_TEST_KEY", "resolved_value");
        try
        {
            Assert.AreEqual("resolved_value", ConfigLayer.ResolveEnvValue("$XLNX_TEST_KEY"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XLNX_TEST_KEY", null);
        }
    }

    [TestMethod]
    public void ResolveEnvValue_MissingEnvRef_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, ConfigLayer.ResolveEnvValue("$NONEXISTENT_VAR_12345"));
    }

    [TestMethod]
    public void ExpandPath_TildeExpands()
    {
        var result = ConfigLayer.ExpandPath("~/workspaces");
        Assert.IsFalse(result.StartsWith("~"));
        Assert.IsTrue(result.Contains("workspaces"));
    }
}
