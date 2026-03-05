using Fluid;
using xLNX.Core.Models;

namespace xLNX.Core.Workflow;

/// <summary>
/// Renders prompt templates using the Fluid (Liquid) template engine.
/// See SPEC Sections 5.4, 12.1–12.4.
/// </summary>
public static class PromptRenderer
{
    private static readonly FluidParser Parser = new();

    /// <summary>
    /// Renders a prompt template with the given issue and attempt context.
    /// </summary>
    public static string Render(string template, Issue issue, int? attempt)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return "You are working on an issue from Linear.";
        }

        if (!Parser.TryParse(template, out var fluidTemplate, out var error))
        {
            throw new WorkflowException("template_parse_error", $"Template parse error: {error}");
        }

        var options = new TemplateOptions();
        options.MemberAccessStrategy.Register<Issue>();
        options.MemberAccessStrategy.Register<BlockerRef>();

        var context = new TemplateContext(options);
        context.SetValue("issue", issue);
        if (attempt.HasValue)
        {
            context.SetValue("attempt", attempt.Value);
        }

        try
        {
            return fluidTemplate.Render(context);
        }
        catch (Exception ex)
        {
            throw new WorkflowException("template_render_error", $"Template render error: {ex.Message}");
        }
    }
}
