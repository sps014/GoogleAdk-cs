using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A tool that wraps an agent, allowing it to be called as a tool by another agent.
/// The wrapped agent runs in its own session with a sub-runner.
/// </summary>
public class AgentTool : BaseTool
{
    private readonly BaseAgent _agent;
    private readonly bool _skipSummarization;

    public AgentTool(BaseAgent agent, bool skipSummarization = false)
        : base(agent.Name, agent.Description ?? string.Empty)
    {
        _agent = agent;
        _skipSummarization = skipSummarization;
    }

    public override FunctionDeclaration? GetDeclaration()
    {
        // If the wrapped agent is an LlmAgent with an InputSchema, use that
        if (_agent is LlmAgent llmAgent && llmAgent.InputSchema != null)
        {
            return new FunctionDeclaration
            {
                Name = Name,
                Description = Description,
                Parameters = llmAgent.InputSchema
            };
        }

        // Default: accept a single "request" string
        return new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["request"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string"
                    }
                },
                ["required"] = new List<string> { "request" }
            }
        };
    }

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        if (_skipSummarization)
        {
            context.EventActions.SkipSummarization = true;
        }

        var hasInputSchema = _agent is LlmAgent la && la.InputSchema != null;
        var requestText = hasInputSchema
            ? System.Text.Json.JsonSerializer.Serialize(args)
            : args.GetValueOrDefault("request")?.ToString() ?? string.Empty;

        var userContent = new Content
        {
            Role = "user",
            Parts = new List<Part>
            {
                new Part { Text = requestText }
            }
        };

        // Create a runner for the sub-agent
        var runner = new Runner.Runner(new Runner.RunnerConfig
        {
            AppName = _agent.Name,
            Agent = _agent,
            SessionService = context.InvocationContext.SessionService!,
            ArtifactService = new ForwardingArtifactService(context),
            MemoryService = context.InvocationContext.MemoryService,
        });

        var session = await runner.SessionService.CreateSessionAsync(new Abstractions.Sessions.CreateSessionRequest
        {
            AppName = _agent.Name,
            UserId = context.UserId,
            SessionId = context.Session.Id,
            State = context.State.ToRecord()
        });

        Event? lastEvent = null;
        await foreach (var evt in runner.RunAsync(session.UserId, session.Id, userContent))
        {
            if (evt.Actions?.StateDelta != null)
            {
                context.State.Update(evt.Actions.StateDelta);
            }
            lastEvent = evt;
        }

        if (lastEvent?.Content?.Parts == null || lastEvent.Content.Parts.Count == 0)
            return string.Empty;

        var mergedText = string.Join("\n",
            lastEvent.Content.Parts
                .Where(p => p.Text != null && p.Thought != true)
                .Select(p => p.Text));

        var hasOutputSchema = _agent is LlmAgent lla && lla.OutputSchema != null;
        if (hasOutputSchema)
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(mergedText);
        }

        return mergedText;
    }
}
