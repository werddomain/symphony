using xLNX.Core.Models;
using xLNX.Core.Orchestration;

namespace xLNX.Tests;

[TestClass]
public class DispatchSorterTests
{
    [TestMethod]
    public void Sort_ByPriorityAscending()
    {
        var issues = new List<Issue>
        {
            new() { Id = "3", Identifier = "C", Priority = 3, CreatedAt = DateTime.UtcNow },
            new() { Id = "1", Identifier = "A", Priority = 1, CreatedAt = DateTime.UtcNow },
            new() { Id = "2", Identifier = "B", Priority = 2, CreatedAt = DateTime.UtcNow }
        };

        var sorted = DispatchSorter.Sort(issues);

        Assert.AreEqual("1", sorted[0].Id);
        Assert.AreEqual("2", sorted[1].Id);
        Assert.AreEqual("3", sorted[2].Id);
    }

    [TestMethod]
    public void Sort_NullPriorityComesLast()
    {
        var issues = new List<Issue>
        {
            new() { Id = "null", Identifier = "N", Priority = null, CreatedAt = DateTime.UtcNow },
            new() { Id = "1", Identifier = "A", Priority = 1, CreatedAt = DateTime.UtcNow }
        };

        var sorted = DispatchSorter.Sort(issues);

        Assert.AreEqual("1", sorted[0].Id);
        Assert.AreEqual("null", sorted[1].Id);
    }

    [TestMethod]
    public void Sort_TiebrokenByCreatedAt()
    {
        var older = DateTime.UtcNow.AddHours(-1);
        var newer = DateTime.UtcNow;

        var issues = new List<Issue>
        {
            new() { Id = "new", Identifier = "N", Priority = 1, CreatedAt = newer },
            new() { Id = "old", Identifier = "O", Priority = 1, CreatedAt = older }
        };

        var sorted = DispatchSorter.Sort(issues);

        Assert.AreEqual("old", sorted[0].Id);
        Assert.AreEqual("new", sorted[1].Id);
    }

    [TestMethod]
    public void Sort_TiebrokenByIdentifier()
    {
        var now = DateTime.UtcNow;

        var issues = new List<Issue>
        {
            new() { Id = "2", Identifier = "B-100", Priority = 1, CreatedAt = now },
            new() { Id = "1", Identifier = "A-100", Priority = 1, CreatedAt = now }
        };

        var sorted = DispatchSorter.Sort(issues);

        Assert.AreEqual("1", sorted[0].Id);
        Assert.AreEqual("2", sorted[1].Id);
    }

    [TestMethod]
    public void Sort_EmptyList_ReturnsEmpty()
    {
        var sorted = DispatchSorter.Sort([]);
        Assert.AreEqual(0, sorted.Count);
    }
}
