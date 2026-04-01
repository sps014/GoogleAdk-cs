// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;

namespace GoogleAdk.E2e.Tests;

public class OrchestrationE2eTests
{
    [Fact]
    public async Task OrchestrationPipeline_ProducesExpectedOutputs()
    {
        var config = LoadConfig();
        Assert.False(config.A2aEnabled);

        var agents = config.Agents.Select(agent =>
            (BaseAgent)new FixedResponseAgent(
                new BaseAgentConfig
                {
                    Name = agent.Name,
                    Description = agent.Description,
                },
                agent.Output)).ToList();

        var pipeline = new SequentialAgent(new BaseAgentConfig
        {
            Name = config.PipelineName,
            Description = "E2E orchestration pipeline.",
            SubAgents = agents,
        });

        var runner = new InMemoryRunner("orchestration-e2e", pipeline);
        var session = await runner.SessionService.CreateSessionAsync(
            new CreateSessionRequest
            {
                AppName = "orchestration-e2e",
                UserId = "user-1",
            });

        var userMessage = new Content
        {
            Role = "user",
            Parts = new List<Part> { new() { Text = config.UserPrompt } }
        };

        var events = new List<OutputEvent>();
        await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
        {
            var text = evt.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text)) continue;
            events.Add(new OutputEvent
            {
                Author = evt.Author ?? string.Empty,
                Text = text,
            });
        }

        var expected = LoadExpected();
        Assert.Equal(expected.Count, events.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Author, events[i].Author);
            Assert.Equal(expected[i].Text, events[i].Text);
        }
    }

    private static OrchestrationConfig LoadConfig()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "orchestration_config.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<OrchestrationConfig>(json, JsonOptions())
               ?? throw new InvalidOperationException("Invalid orchestration_config.json.");
    }

    private static List<OutputEvent> LoadExpected()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "orchestration_expected.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<OutputEvent>>(json, JsonOptions())
               ?? new List<OutputEvent>();
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private sealed class FixedResponseAgent : BaseAgent
    {
        private readonly string _response;

        public FixedResponseAgent(BaseAgentConfig config, string response) : base(config)
        {
            _response = response;
        }

        protected override async IAsyncEnumerable<GoogleAdk.Core.Abstractions.Events.Event> RunAsyncImpl(
            InvocationContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return GoogleAdk.Core.Abstractions.Events.Event.Create(e =>
            {
                e.Author = Name;
                e.InvocationId = context.InvocationId;
                e.Content = new Content
                {
                    Role = "model",
                    Parts = new List<Part> { new() { Text = _response } },
                };
            });
            await Task.CompletedTask;
        }
    }

    private sealed record OrchestrationConfig(
        string PipelineName,
        bool A2aEnabled,
        string UserPrompt,
        List<AgentConfig> Agents);

    private sealed record AgentConfig(
        string Name,
        string Description,
        string Output);

    private sealed record OutputEvent
    {
        public string Author { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
    }
}

