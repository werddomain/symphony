using xLNX.Core.Models;
using xLNX.Core.Workspaces;

namespace xLNX.Tests;

[TestClass]
public class WorkspaceManagerTests
{
    [TestMethod]
    public void SanitizeIdentifier_AlphanumericUnchanged()
    {
        Assert.AreEqual("ABC-123", WorkspaceManager.SanitizeIdentifier("ABC-123"));
    }

    [TestMethod]
    public void SanitizeIdentifier_SpecialCharsReplaced()
    {
        Assert.AreEqual("ABC_123_test", WorkspaceManager.SanitizeIdentifier("ABC/123 test"));
    }

    [TestMethod]
    public void SanitizeIdentifier_DotsAndUnderscoresPreserved()
    {
        Assert.AreEqual("file.name_v2", WorkspaceManager.SanitizeIdentifier("file.name_v2"));
    }

    [TestMethod]
    public void SanitizeIdentifier_AllSpecialChars()
    {
        Assert.AreEqual("___", WorkspaceManager.SanitizeIdentifier("@#$"));
    }

    [TestMethod]
    public async Task CreateForIssue_DeterministicPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xlnx_test_{Guid.NewGuid():N}");
        try
        {
            var config = new ServiceConfig { WorkspaceRoot = root };
            var mgr = new WorkspaceManager(() => config,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceManager>());

            var ws = await mgr.CreateForIssueAsync("TEST-42");

            Assert.AreEqual("TEST-42", ws.WorkspaceKey);
            Assert.IsTrue(ws.Path.StartsWith(Path.GetFullPath(root)));
            Assert.IsTrue(ws.CreatedNow);
            Assert.IsTrue(Directory.Exists(ws.Path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task CreateForIssue_ExistingDirectory_Reused()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xlnx_test_{Guid.NewGuid():N}");
        try
        {
            var config = new ServiceConfig { WorkspaceRoot = root };
            var mgr = new WorkspaceManager(() => config,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceManager>());

            var ws1 = await mgr.CreateForIssueAsync("TEST-42");
            Assert.IsTrue(ws1.CreatedNow);

            var ws2 = await mgr.CreateForIssueAsync("TEST-42");
            Assert.IsFalse(ws2.CreatedNow);
            Assert.AreEqual(ws1.Path, ws2.Path);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task CreateForIssue_PathContainment_Enforced()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xlnx_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var config = new ServiceConfig { WorkspaceRoot = root };
            var mgr = new WorkspaceManager(() => config,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceManager>());

            // Normal identifier should work
            var ws = await mgr.CreateForIssueAsync("SAFE-123");
            Assert.IsTrue(ws.Path.StartsWith(Path.GetFullPath(root)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task RemoveWorkspace_CleansDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xlnx_test_{Guid.NewGuid():N}");
        try
        {
            var config = new ServiceConfig { WorkspaceRoot = root };
            var mgr = new WorkspaceManager(() => config,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceManager>());

            var ws = await mgr.CreateForIssueAsync("TEST-99");
            Assert.IsTrue(Directory.Exists(ws.Path));

            await mgr.RemoveWorkspaceAsync("TEST-99");
            Assert.IsFalse(Directory.Exists(ws.Path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task RemoveWorkspace_NonExistentDirectory_NoError()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xlnx_test_{Guid.NewGuid():N}");
        try
        {
            var config = new ServiceConfig { WorkspaceRoot = root };
            var mgr = new WorkspaceManager(() => config,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceManager>());

            // Should not throw
            await mgr.RemoveWorkspaceAsync("NONEXISTENT-1");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
