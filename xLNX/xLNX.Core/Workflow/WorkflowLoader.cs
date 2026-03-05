using xLNX.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace xLNX.Core.Workflow;

/// <summary>
/// Reads WORKFLOW.md and parses YAML front matter + prompt body.
/// See SPEC Sections 5.1 and 5.2.
/// </summary>
public static class WorkflowLoader
{
    private const string FrontMatterDelimiter = "---";

    /// <summary>
    /// Loads a workflow definition from the given file path.
    /// </summary>
    public static WorkflowDefinition Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new WorkflowException("missing_workflow_file", $"Workflow file not found: {filePath}");
        }

        string content;
        try
        {
            content = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            throw new WorkflowException("missing_workflow_file", $"Cannot read workflow file: {ex.Message}");
        }

        return Parse(content);
    }

    /// <summary>
    /// Parses workflow content (YAML front matter + markdown body).
    /// </summary>
    public static WorkflowDefinition Parse(string content)
    {
        var config = new Dictionary<string, object?>();
        string promptBody;

        if (content.TrimStart().StartsWith(FrontMatterDelimiter))
        {
            var lines = content.Split('\n');
            int startIdx = -1;
            int endIdx = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == FrontMatterDelimiter)
                {
                    if (startIdx == -1)
                    {
                        startIdx = i;
                    }
                    else
                    {
                        endIdx = i;
                        break;
                    }
                }
            }

            if (startIdx != -1 && endIdx != -1)
            {
                var yamlLines = lines.Skip(startIdx + 1).Take(endIdx - startIdx - 1);
                var yamlContent = string.Join('\n', yamlLines);

                try
                {
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .Build();

                    var parsed = deserializer.Deserialize<Dictionary<string, object?>>(yamlContent);
                    if (parsed != null)
                    {
                        config = parsed;
                    }
                    else
                    {
                        // Empty YAML is valid, treat as empty config
                    }
                }
                catch (Exception ex)
                {
                    throw new WorkflowException("workflow_parse_error", $"Invalid YAML front matter: {ex.Message}");
                }

                promptBody = string.Join('\n', lines.Skip(endIdx + 1)).Trim();
            }
            else
            {
                // No closing delimiter, entire content is prompt
                promptBody = content.Trim();
            }
        }
        else
        {
            promptBody = content.Trim();
        }

        return new WorkflowDefinition
        {
            Config = config,
            PromptTemplate = promptBody
        };
    }
}

/// <summary>
/// Exception for workflow loading errors.
/// </summary>
public class WorkflowException : Exception
{
    public string ErrorCode { get; }

    public WorkflowException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
