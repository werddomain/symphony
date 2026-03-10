using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using xLNX.Core.Models;

namespace xLNX.Core.Tracker;

/// <summary>
/// Linear issue tracker client. See SPEC Sections 11.2–11.4.
/// </summary>
public class LinearClient : IIssueTrackerClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinearClient> _logger;
    private readonly Func<ServiceConfig> _configProvider;
    private const int PageSize = 50;
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromMilliseconds(30_000);

    public LinearClient(HttpClient httpClient, ILogger<LinearClient> logger, Func<ServiceConfig> configProvider)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = NetworkTimeout;
        _logger = logger;
        _configProvider = configProvider;
    }

    public async Task<List<Issue>> FetchCandidateIssuesAsync(CancellationToken ct = default)
    {
        var config = _configProvider();
        var allIssues = new List<Issue>();
        string? cursor = null;

        do
        {
            var query = BuildCandidateQuery(config, cursor);
            var response = await ExecuteGraphQLAsync(config, query, ct);
            var issues = NormalizeIssueNodes(response);
            allIssues.AddRange(issues.Nodes);

            cursor = issues.HasNextPage ? issues.EndCursor : null;
        } while (cursor != null);

        return allIssues;
    }

    public async Task<List<Issue>> FetchIssuesByStatesAsync(IReadOnlyList<string> stateNames, CancellationToken ct = default)
    {
        if (stateNames.Count == 0) return [];

        var config = _configProvider();
        var query = BuildIssuesByStatesQuery(config, stateNames);
        var response = await ExecuteGraphQLAsync(config, query, ct);
        return NormalizeIssueNodes(response).Nodes;
    }

    public async Task<List<Issue>> FetchIssueStatesByIdsAsync(IReadOnlyList<string> issueIds, CancellationToken ct = default)
    {
        if (issueIds.Count == 0) return [];

        var config = _configProvider();
        var query = BuildIssueStateRefreshQuery(issueIds);
        var response = await ExecuteGraphQLAsync(config, query, ct);
        return NormalizeStateRefreshNodes(response);
    }

    private static object BuildCandidateQuery(ServiceConfig config, string? cursor)
    {
        var variables = new Dictionary<string, object?>
        {
            ["projectSlug"] = config.TrackerProjectSlug,
            ["states"] = config.ActiveStates,
            ["first"] = PageSize
        };

        if (cursor != null) variables["after"] = cursor;

        return new
        {
            query = """
                query($projectSlug: String!, $states: [String!]!, $first: Int!, $after: String) {
                  issues(
                    filter: {
                      project: { slugId: { eq: $projectSlug } }
                      state: { name: { in: $states } }
                    }
                    first: $first
                    after: $after
                  ) {
                    nodes {
                      id identifier title description priority
                      state { name }
                      branchName url
                      labels { nodes { name } }
                      inverseRelations { nodes { type relatedIssue { id identifier state { name } } } }
                      createdAt updatedAt
                    }
                    pageInfo { hasNextPage endCursor }
                  }
                }
                """,
            variables
        };
    }

    private static object BuildIssuesByStatesQuery(ServiceConfig config, IReadOnlyList<string> stateNames)
    {
        return new
        {
            query = """
                query($projectSlug: String!, $states: [String!]!) {
                  issues(
                    filter: {
                      project: { slugId: { eq: $projectSlug } }
                      state: { name: { in: $states } }
                    }
                  ) {
                    nodes { id identifier state { name } }
                  }
                }
                """,
            variables = new { projectSlug = config.TrackerProjectSlug, states = stateNames }
        };
    }

    private static object BuildIssueStateRefreshQuery(IReadOnlyList<string> issueIds)
    {
        return new
        {
            query = """
                query($ids: [ID!]!) {
                  nodes(ids: $ids) {
                    ... on Issue { id identifier state { name } }
                  }
                }
                """,
            variables = new { ids = issueIds }
        };
    }

    private async Task<JsonElement> ExecuteGraphQLAsync(ServiceConfig config, object query, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(query);
        using var request = new HttpRequestMessage(HttpMethod.Post, config.TrackerEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.TrackerApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            throw new TrackerException("linear_api_request", $"Linear API request failed: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new TrackerException("linear_api_status", $"Linear API returned {(int)response.StatusCode}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonSerializer.Deserialize<JsonElement>(body);

        if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
        {
            throw new TrackerException("linear_graphql_errors", $"GraphQL errors: {errors}");
        }

        if (!doc.TryGetProperty("data", out var data))
        {
            throw new TrackerException("linear_unknown_payload", "Missing data in GraphQL response");
        }

        return data;
    }

    private static (List<Issue> Nodes, bool HasNextPage, string? EndCursor) NormalizeIssueNodes(JsonElement data)
    {
        var nodes = new List<Issue>();
        if (!data.TryGetProperty("issues", out var issues) || !issues.TryGetProperty("nodes", out var issueNodes))
            return (nodes, false, null);

        foreach (var node in issueNodes.EnumerateArray())
        {
            nodes.Add(NormalizeIssue(node));
        }

        bool hasNextPage = false;
        string? endCursor = null;
        if (issues.TryGetProperty("pageInfo", out var pageInfo))
        {
            hasNextPage = pageInfo.TryGetProperty("hasNextPage", out var hnp) && hnp.GetBoolean();
            endCursor = pageInfo.TryGetProperty("endCursor", out var ec) ? ec.GetString() : null;
        }

        return (nodes, hasNextPage, endCursor);
    }

    private static List<Issue> NormalizeStateRefreshNodes(JsonElement data)
    {
        var results = new List<Issue>();
        if (!data.TryGetProperty("nodes", out var nodes))
            return results;

        foreach (var node in nodes.EnumerateArray())
        {
            if (node.TryGetProperty("id", out var idProp))
            {
                results.Add(new Issue
                {
                    Id = idProp.GetString() ?? string.Empty,
                    Identifier = node.TryGetProperty("identifier", out var ident) ? ident.GetString() ?? string.Empty : string.Empty,
                    State = node.TryGetProperty("state", out var state) && state.TryGetProperty("name", out var sn) ? sn.GetString() ?? string.Empty : string.Empty
                });
            }
        }

        return results;
    }

    private static Issue NormalizeIssue(JsonElement node)
    {
        var issue = new Issue
        {
            Id = node.GetProperty("id").GetString() ?? string.Empty,
            Identifier = node.TryGetProperty("identifier", out var ident) ? ident.GetString() ?? string.Empty : string.Empty,
            Title = node.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
            Description = node.TryGetProperty("description", out var desc) ? desc.GetString() : null,
            State = node.TryGetProperty("state", out var state) && state.TryGetProperty("name", out var sn) ? sn.GetString() ?? string.Empty : string.Empty,
            BranchName = node.TryGetProperty("branchName", out var bn) ? bn.GetString() : null,
            Url = node.TryGetProperty("url", out var url) ? url.GetString() : null,
        };

        // Priority - integer only
        if (node.TryGetProperty("priority", out var priority) && priority.ValueKind == JsonValueKind.Number)
        {
            issue.Priority = priority.GetInt32();
        }

        // Labels - normalize to lowercase
        if (node.TryGetProperty("labels", out var labels) && labels.TryGetProperty("nodes", out var labelNodes))
        {
            foreach (var label in labelNodes.EnumerateArray())
            {
                if (label.TryGetProperty("name", out var name))
                {
                    issue.Labels.Add((name.GetString() ?? string.Empty).ToLowerInvariant());
                }
            }
        }

        // BlockedBy - from inverse relations where type is "blocks"
        if (node.TryGetProperty("inverseRelations", out var relations) && relations.TryGetProperty("nodes", out var relNodes))
        {
            foreach (var rel in relNodes.EnumerateArray())
            {
                if (rel.TryGetProperty("type", out var type) && type.GetString() == "blocks"
                    && rel.TryGetProperty("relatedIssue", out var related))
                {
                    issue.BlockedBy.Add(new BlockerRef
                    {
                        Id = related.TryGetProperty("id", out var bid) ? bid.GetString() : null,
                        Identifier = related.TryGetProperty("identifier", out var bident) ? bident.GetString() : null,
                        State = related.TryGetProperty("state", out var bstate) && bstate.TryGetProperty("name", out var bsn) ? bsn.GetString() : null
                    });
                }
            }
        }

        // Timestamps
        if (node.TryGetProperty("createdAt", out var createdAt) && DateTime.TryParse(createdAt.GetString(), out var ca))
        {
            issue.CreatedAt = ca;
        }
        if (node.TryGetProperty("updatedAt", out var updatedAt) && DateTime.TryParse(updatedAt.GetString(), out var ua))
        {
            issue.UpdatedAt = ua;
        }

        return issue;
    }
}

public class TrackerException : Exception
{
    public string ErrorCode { get; }

    public TrackerException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
