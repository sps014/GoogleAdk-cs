using GoogleAdk.Core.A2a;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Sessions;
using GoogleAdk.ApiServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace GoogleAdk.E2e.Tests;

public class A2aE2eTests
{
    [Fact]
    public async Task A2aClient_CanCommunicateWith_A2aRemoteAgent_OverRest()
    {
        const string appName = "a2a-e2e";

        // 1. Setup a TestServer-hosted WebApplication to serve A2A endpoints.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var loader = new AgentLoader(Path.GetTempPath());
        loader.Register(appName, new SimpleTestAgent());
        builder.Services.AddSingleton(loader);
        builder.Services.AddSingleton<BaseSessionService, InMemorySessionService>();
        builder.Services.AddSingleton<RunnerManager>();

        await using var app = builder.Build();
        app.MapA2aApi();
        await app.StartAsync();
        var client = app.GetTestClient();

        // 2. Setup A2aClient using the HttpClient from the TestServer
        var baseUrl = new Uri(client.BaseAddress!, $"a2a/{appName}/rest").ToString().TrimEnd('/');
        var a2aClient = new A2aClient(baseUrl, "HTTP+JSON", client);
        var agentCard = new AgentCard
        {
            Name = appName,
            Url = baseUrl,
            PreferredTransport = "HTTP+JSON",
            Capabilities = new AgentCapabilities { Streaming = false },
        };

        // 3. Setup the RemoteA2aAgent to use the A2aClient
        var remoteAgent = new RemoteA2aAgent(new RemoteA2aAgentConfig
        {
            Name = "remote-test-agent",
            Client = a2aClient,
            AgentCard = agentCard,
        });

        // 4. Run the remote agent with some input
        var session = Session.Create("session-1", appName, "user-1");
        var request = new Event
        {
            Author = MessageRole.User,
            Content = new Content
            {
                Role = "user",
                Parts = [new Part { Text = "Hello from E2E" }],
            }
        };
        session.Events.Add(request);
        var invocationContext = new InvocationContext { Session = session };

        var responseParts = new List<Part>();
        await foreach (var @event in remoteAgent.RunAsync(invocationContext))
        {
            responseParts.AddRange(@event.Content?.Parts ?? []);
        }

        Assert.NotEmpty(responseParts);
        Assert.Equal("Reply from SimpleTestAgent: Hello from E2E", responseParts.First().Text);
        await app.StopAsync();
    }

    private sealed class SimpleTestAgent : BaseAgent
    {
        public SimpleTestAgent() : base(new BaseAgentConfig { Name = "SimpleTestAgent" })
        {
        }

        protected override async IAsyncEnumerable<Event> RunAsyncImpl(
            InvocationContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var request = context.Session.Events.LastOrDefault();
            var userInput = request?.Content?.Parts?.FirstOrDefault()?.Text ?? "No input";
            yield return Event.Create(e =>
            {
                e.Author = Name;
                e.Content = new Content
                {
                    Role = "model",
                    Parts = [new Part { Text = $"Reply from SimpleTestAgent: {userInput}" }]
                };
            });
            await Task.CompletedTask;
        }
    }
}
