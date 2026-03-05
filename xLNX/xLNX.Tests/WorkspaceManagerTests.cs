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
}
