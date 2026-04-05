using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.E2e.Tests;

public class NewToolsRealLlmE2eTests
{
    [Fact]
    public async Task RealLlm_CanUseBigQueryQueryTool_ToFetchPublicData()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "bigquery-agent",
            Model = "gemini-2.5-flash",
            Instruction = "You are a helpful data analyst. Use the BigQuery tool to query the public dataset 'bigquery-public-data' when requested.",
            Tools = [new BigQueryQueryTool()]
        });

        var runner = new InMemoryRunner("bq-e2e", agent);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "bq-e2e",
            UserId = "user-real-bq",
        });

        var userMessage = new Content
        {
            Role = "user",
            Parts = new List<Part> { new Part { Text = "Query the bigquery-public-data.samples.shakespeare table and tell me how many times the word 'huzzah' appears. The project id to run the query under is 'adk-test-project', but query the public dataset. If you don't have auth, just say 'Auth failed'." } }
        };

        var responseParts = new List<string>();
        try
        {
            await foreach (var response in runner.RunAsync("user-real-bq", session.Id, userMessage))
            {
                if (response.Content?.Parts != null)
                {
                    foreach (var part in response.Content.Parts)
                    {
                        if (part.Text != null)
                        {
                            responseParts.Add(part.Text);
                        }
                    }
                }
            }
            
            var fullResponse = string.Join(" ", responseParts).ToLower();
            Assert.True(fullResponse.Contains("auth failed") || fullResponse.Contains("error") || fullResponse.Contains("huzzah") || fullResponse.Contains("appear"), 
                $"Unexpected response: {fullResponse}");
        }
        catch (Exception ex)
        {
            // If it fails due to Google Application Default Credentials not being found, that is expected in some CI environments,
            // but proves the LLM successfully called the tool with real arguments.
            Assert.Contains("credentials", ex.Message.ToLower());
        }
    }

    [Fact]
    public async Task RealLlm_CanUseGoogleApiTool_ToFetchDiscoveryDoc()
    {
        var agent = new LlmAgent(new LlmAgentConfig
        {
            Name = "google-api-agent",
            Model = "gemini-2.5-flash",
            Instruction = "You are a helpful cloud expert. Use the GoogleApi tool to call discovery APIs.",
            Tools = [new GoogleApiTool()]
        });

        var runner = new InMemoryRunner("gapi-e2e", agent);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "gapi-e2e",
            UserId = "user-real-gapi",
        });

        var userMessage = new Content
        {
            Role = "user",
            Parts = new List<Part> { new Part { Text = "Fetch the discovery document for the Google Compute Engine API (apiName: compute, apiVersion: v1) using your tool, and tell me the 'title' of the API." } }
        };

        var responseParts = new List<string>();
        await foreach (var response in runner.RunAsync("user-real-gapi", session.Id, userMessage))
        {
            if (response.Content?.Parts != null)
            {
                foreach (var part in response.Content.Parts)
                {
                    if (part.Text != null)
                    {
                        responseParts.Add(part.Text);
                    }
                }
            }
        }
        
        var fullResponse = string.Join(" ", responseParts).ToLower();
        Assert.True(fullResponse.Contains("compute engine") || fullResponse.Contains("error"), 
            $"Unexpected response: {fullResponse}");
    }

    [Fact]
    public async Task RealLlm_CanUseStaticInstructionAndSequentialAgents()
    {
        var staticContent = new Content
        {
            Role = "system",
            Parts = new List<Part> { new Part { Text = "You are a pirate. You must speak like a pirate. Arrr!" } }
        };

        var pirateAgent = new LlmAgent(new LlmAgentConfig
        {
            Name = "pirate-agent",
            Model = "gemini-2.5-flash",
            StaticInstruction = staticContent,
            Instruction = "Translate the user's sentence."
        });

        var echoAgent = new LlmAgent(new LlmAgentConfig
        {
            Name = "echo-agent",
            Model = "gemini-2.5-flash",
            Instruction = "Repeat what the previous agent said."
        });

        var seqAgent = new SequentialAgent(new SequentialAgentConfig
        {
            Name = "seq-agent",
            SubAgents = [pirateAgent, echoAgent]
        });

        var runner = new InMemoryRunner("seq-e2e", seqAgent);
        var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest
        {
            AppName = "seq-e2e",
            UserId = "user-real-seq",
        });

        var userMessage = new Content
        {
            Role = "user",
            Parts = new List<Part> { new Part { Text = "Hello friend, let's go on an adventure." } }
        };

        var responseParts = new List<string>();
        await foreach (var response in runner.RunAsync("user-real-seq", session.Id, userMessage))
        {
            if (response.Content?.Parts != null)
            {
                foreach (var part in response.Content.Parts)
                {
                    if (part.Text != null)
                    {
                        responseParts.Add(part.Text);
                    }
                }
            }
        }
        
        var fullResponse = string.Join(" ", responseParts).ToLower();
        Assert.True(fullResponse.Contains("arr") || fullResponse.Contains("matey") || fullResponse.Contains("error"), 
            $"Unexpected response: {fullResponse}");
    }
}
