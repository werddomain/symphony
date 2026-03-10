using System.Net;
using System.Text;
using System.Text.Json;
using xLNX.Core.Models;
using xLNX.Core.Tracker;

namespace xLNX.Tests;

/// <summary>
/// Tests for LinearClient (SPEC Section 17.3).
/// Uses a fake HTTP message handler to mock Linear API responses.
/// </summary>
[TestClass]
public class LinearClientTests
{
    private static ServiceConfig TestConfig() => new()
    {
        TrackerKind = "linear",
        TrackerApiKey = "test-key",
        TrackerProjectSlug = "my-project",
        TrackerEndpoint = "https://api.linear.app/graphql",
        ActiveStates = ["Todo", "In Progress"],
        TerminalStates = ["Done", "Cancelled"]
    };

    private static LinearClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var fakeHandler = new FakeHttpMessageHandler(handler);
        var httpClient = new HttpClient(fakeHandler);
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<LinearClient>();
        return new LinearClient(httpClient, logger, TestConfig);
    }

    [TestMethod]
    public async Task FetchCandidateIssues_ReturnsNormalizedIssues()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                data = new
                {
                    issues = new
                    {
                        nodes = new[]
                        {
                            new
                            {
                                id = "id-1",
                                identifier = "TEST-1",
                                title = "Issue One",
                                description = "Desc",
                                priority = 1,
                                state = new { name = "Todo" },
                                branchName = (string?)null,
                                url = "https://linear.app/issue/TEST-1",
                                labels = new { nodes = new[] { new { name = "Bug" } } },
                                inverseRelations = new { nodes = Array.Empty<object>() },
                                createdAt = "2025-01-01T00:00:00Z",
                                updatedAt = "2025-01-02T00:00:00Z"
                            }
                        },
                        pageInfo = new { hasNextPage = false, endCursor = (string?)null }
                    }
                }
            }), Encoding.UTF8, "application/json")
        });

        var result = await client.FetchCandidateIssuesAsync();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("id-1", result[0].Id);
        Assert.AreEqual("TEST-1", result[0].Identifier);
        Assert.AreEqual("Issue One", result[0].Title);
        Assert.AreEqual("Todo", result[0].State);
        Assert.AreEqual(1, result[0].Priority);
        Assert.AreEqual(1, result[0].Labels.Count);
        Assert.AreEqual("bug", result[0].Labels[0]); // Lowercase normalized
    }

    [TestMethod]
    public async Task FetchCandidateIssues_PaginationPreservesOrder()
    {
        int callCount = 0;
        var client = CreateClient(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        data = new
                        {
                            issues = new
                            {
                                nodes = new[] { new { id = "id-1", identifier = "TEST-1", title = "First", state = new { name = "Todo" }, labels = new { nodes = Array.Empty<object>() }, inverseRelations = new { nodes = Array.Empty<object>() } } },
                                pageInfo = new { hasNextPage = true, endCursor = "cursor-1" }
                            }
                        }
                    }), Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    data = new
                    {
                        issues = new
                        {
                            nodes = new[] { new { id = "id-2", identifier = "TEST-2", title = "Second", state = new { name = "In Progress" }, labels = new { nodes = Array.Empty<object>() }, inverseRelations = new { nodes = Array.Empty<object>() } } },
                            pageInfo = new { hasNextPage = false, endCursor = (string?)null }
                        }
                    }
                }), Encoding.UTF8, "application/json")
            };
        });

        var result = await client.FetchCandidateIssuesAsync();

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("id-1", result[0].Id);
        Assert.AreEqual("id-2", result[1].Id);
    }

    [TestMethod]
    public async Task FetchIssuesByStates_EmptyStates_ReturnsEmpty()
    {
        bool apiCalled = false;
        var client = CreateClient(_ =>
        {
            apiCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await client.FetchIssuesByStatesAsync([]);

        Assert.AreEqual(0, result.Count);
        Assert.IsFalse(apiCalled); // No API call for empty states
    }

    [TestMethod]
    public async Task FetchIssueStatesByIds_ReturnsMinimalIssues()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                data = new
                {
                    nodes = new[]
                    {
                        new { id = "id-1", identifier = "TEST-1", state = new { name = "Done" } },
                        new { id = "id-2", identifier = "TEST-2", state = new { name = "In Progress" } }
                    }
                }
            }), Encoding.UTF8, "application/json")
        });

        var result = await client.FetchIssueStatesByIdsAsync(["id-1", "id-2"]);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Done", result[0].State);
        Assert.AreEqual("In Progress", result[1].State);
    }

    [TestMethod]
    public async Task FetchIssueStatesByIds_EmptyIds_ReturnsEmpty()
    {
        bool apiCalled = false;
        var client = CreateClient(_ =>
        {
            apiCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await client.FetchIssueStatesByIdsAsync([]);

        Assert.AreEqual(0, result.Count);
        Assert.IsFalse(apiCalled);
    }

    [TestMethod]
    public async Task FetchIssueStatesByIds_UsesGraphQLIDTyping()
    {
        string? capturedBody = null;
        var client = CreateClient(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    data = new { nodes = Array.Empty<object>() }
                }), Encoding.UTF8, "application/json")
            };
        });

        await client.FetchIssueStatesByIdsAsync(["id-1"]);

        Assert.IsNotNull(capturedBody);
        // Verify the query uses [ID!]! typing per SPEC 11.2
        Assert.IsTrue(capturedBody.Contains("[ID!]!"));
    }

    [TestMethod]
    public async Task FetchCandidateIssues_UsesSlugIdFilter()
    {
        string? capturedBody = null;
        var client = CreateClient(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    data = new
                    {
                        issues = new
                        {
                            nodes = Array.Empty<object>(),
                            pageInfo = new { hasNextPage = false, endCursor = (string?)null }
                        }
                    }
                }), Encoding.UTF8, "application/json")
            };
        });

        await client.FetchCandidateIssuesAsync();

        Assert.IsNotNull(capturedBody);
        // Verify the query uses slugId filter per SPEC 11.2
        Assert.IsTrue(capturedBody.Contains("slugId"));
    }

    [TestMethod]
    public async Task FetchCandidateIssues_NormalizesBlockersFromInverseRelations()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                data = new
                {
                    issues = new
                    {
                        nodes = new[]
                        {
                            new
                            {
                                id = "id-1",
                                identifier = "TEST-1",
                                title = "Blocked",
                                state = new { name = "Todo" },
                                labels = new { nodes = Array.Empty<object>() },
                                inverseRelations = new
                                {
                                    nodes = new[]
                                    {
                                        new
                                        {
                                            type = "blocks",
                                            relatedIssue = new { id = "blocker-1", identifier = "BLOCK-1", state = new { name = "In Progress" } }
                                        }
                                    }
                                }
                            }
                        },
                        pageInfo = new { hasNextPage = false, endCursor = (string?)null }
                    }
                }
            }), Encoding.UTF8, "application/json")
        });

        var result = await client.FetchCandidateIssuesAsync();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(1, result[0].BlockedBy.Count);
        Assert.AreEqual("blocker-1", result[0].BlockedBy[0].Id);
        Assert.AreEqual("BLOCK-1", result[0].BlockedBy[0].Identifier);
        Assert.AreEqual("In Progress", result[0].BlockedBy[0].State);
    }

    [TestMethod]
    public async Task FetchCandidateIssues_LabelsNormalizedToLowercase()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                data = new
                {
                    issues = new
                    {
                        nodes = new[]
                        {
                            new
                            {
                                id = "id-1",
                                identifier = "TEST-1",
                                title = "Issue",
                                state = new { name = "Todo" },
                                labels = new { nodes = new[] { new { name = "Bug" }, new { name = "HIGH-PRIORITY" } } },
                                inverseRelations = new { nodes = Array.Empty<object>() }
                            }
                        },
                        pageInfo = new { hasNextPage = false, endCursor = (string?)null }
                    }
                }
            }), Encoding.UTF8, "application/json")
        });

        var result = await client.FetchCandidateIssuesAsync();

        Assert.AreEqual(2, result[0].Labels.Count);
        Assert.AreEqual("bug", result[0].Labels[0]);
        Assert.AreEqual("high-priority", result[0].Labels[1]);
    }

    [TestMethod]
    public async Task FetchCandidateIssues_RequestError_ThrowsTrackerException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("Network error"));

        var ex = await Assert.ThrowsExactlyAsync<TrackerException>(
            () => client.FetchCandidateIssuesAsync());
        Assert.AreEqual("linear_api_request", ex.ErrorCode);
    }

    [TestMethod]
    public async Task FetchCandidateIssues_Non200_ThrowsTrackerException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsExactlyAsync<TrackerException>(
            () => client.FetchCandidateIssuesAsync());
        Assert.AreEqual("linear_api_status", ex.ErrorCode);
    }

    [TestMethod]
    public async Task FetchCandidateIssues_GraphQLErrors_ThrowsTrackerException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                errors = new[] { new { message = "Something went wrong" } }
            }), Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsExactlyAsync<TrackerException>(
            () => client.FetchCandidateIssuesAsync());
        Assert.AreEqual("linear_graphql_errors", ex.ErrorCode);
    }

    [TestMethod]
    public async Task FetchCandidateIssues_MalformedPayload_ThrowsTrackerException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                result = "unexpected shape"
            }), Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsExactlyAsync<TrackerException>(
            () => client.FetchCandidateIssuesAsync());
        Assert.AreEqual("linear_unknown_payload", ex.ErrorCode);
    }

    [TestMethod]
    public async Task FetchCandidateIssues_BearerTokenIncluded()
    {
        string? authHeader = null;
        var client = CreateClient(req =>
        {
            authHeader = req.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    data = new
                    {
                        issues = new
                        {
                            nodes = Array.Empty<object>(),
                            pageInfo = new { hasNextPage = false, endCursor = (string?)null }
                        }
                    }
                }), Encoding.UTF8, "application/json")
            };
        });

        await client.FetchCandidateIssuesAsync();

        Assert.IsNotNull(authHeader);
        Assert.AreEqual("Bearer test-key", authHeader);
    }
}

/// <summary>
/// Fake HTTP message handler for testing.
/// </summary>
internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
