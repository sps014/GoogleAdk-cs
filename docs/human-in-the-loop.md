# Human in the Loop (Confirmation)

When connecting agents to external databases or sensitive APIs, allowing an LLM to blindly execute actions (like deleting a record, updating a billing amount, or initiating a refund) introduces unacceptable risks.

The ADK resolves this via the **Human-in-the-Loop** confirmation pattern, enabling you to pause execution immediately prior to a dangerous tool call, surface the action to a user or admin, and resume only upon explicit approval.

## Implementation

The confirmation pattern is a dialogue loop: the agent emits a `ConfirmationRequest` event, execution suspends, and the UI responds with a `ToolConfirmation` object dictating acceptance or rejection.

```csharp
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Agents;
using GoogleAdk.Models.Gemini;

// 1. Configure the agent with potentially sensitive tools
var model = GeminiModelFactory.Create("gemini-2.5-flash");
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "time_off_agent",
    Model = model,
    Instruction = "Use the 'request_time_off' tool for time off requests.",
    Tools = [ RequireConfirmationTools.RequestTimeOffTool ]
});

var runner = new InMemoryRunner("hitl-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest { AppName = "hitl-sample", UserId = "user-1" });

var userMessage = new Content { Role = "user", Parts = [new Part { Text = "Request 5 days off." }] };

// 2. Execute. The agent will attempt to call the request_time_off tool and pause.
await foreach (var evt in runner.RunAsync("user-1", session.Id, userMessage))
{
    foreach (var part in evt.Content.Parts)
    {
        // 3. Catch the special RequestConfirmation tool
        if (part.FunctionCall?.Name == FunctionCallHandler.RequestConfirmationFunctionCallName)
        {
            var originalFunctionCallId = GetOriginalFunctionCallId(part.FunctionCall);
            var confirmationId = part.FunctionCall.Id;

            // Pause here, prompt the user via UI/Console, and gather their boolean choice (Accepted)
            bool accepted = PromptUserForApproval(); 

            // 4. Construct the resumption response
            var confirmationResponse = new Content
            {
                Role = "user",
                Parts =
                [
                    new Part
                    {
                        FunctionResponse = new FunctionResponse
                        {
                            Name = FunctionCallHandler.RequestConfirmationFunctionCallName,
                            Id = confirmationId,
                            Response = new Dictionary<string, object?>
                            {
                                ["toolConfirmation"] = new ToolConfirmation
                                {
                                    FunctionCallId = originalFunctionCallId,
                                    Accepted = accepted
                                }
                            }
                        }
                    }
                ]
            };

            // 5. Resume execution by passing the confirmation response back into RunAsync
            await RunOnceAsync(runner, session.Id, confirmationResponse);
        }
    }
}


/// <summary>Request time off for the employee.</summary>
/// <param name="days">Number of days requested.</param>
/// <param name="context">Agent context used for confirmation flow.</param>
[FunctionTool(Name = "request_time_off", RequireConfirmation = true)]
static object? RequestTimeOff(int days, AgentContext context)
{
    if (days <= 0)
        return new Dictionary<string, object?> { ["status"] = "Invalid days to request." };

    return new Dictionary<string, object?>
    {
        ["status"] = "pending_approval",
        ["requested_days"] = days
    };
}
```

If `Accepted` is true, the ADK will proceed to invoke the underlying sensitive tool and relay the result to the LLM. If false, it blocks execution and informs the LLM that the user denied the request.