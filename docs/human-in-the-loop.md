# Human in the Loop (Confirmation)

When connecting agents to external databases or sensitive APIs, allowing an LLM to blindly execute actions (like deleting a record, updating a billing amount, or initiating a refund) introduces unacceptable risks.

The ADK resolves this via the **Human-in-the-Loop** confirmation pattern, enabling you to pause execution immediately prior to a dangerous tool call, surface the action to a user or admin, and resume only upon explicit approval.

## Implementation

The confirmation pattern is a dialogue loop: the agent emits a `ConfirmationRequest` event, execution suspends, and the UI responds with a `ToolConfirmation` object dictating acceptance or rejection.

```csharp
using GoogleAdk.Core.Runner;
using GoogleAdk.Core.Agents;

// 1. Configure the agent with potentially sensitive tools
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "time_off_agent",
    Model = "gemini-2.5-flash",
    Instruction = "Use the 'reimburse' tool for expense reimbursements.",
    Tools = [ RequireConfirmationTools.ReimburseTool ] // Requires confirmation via Security Plugin
});

var runner = new InMemoryRunner("hitl-sample", agent);
var session = await runner.SessionService.CreateSessionAsync(new CreateSessionRequest { AppName = "hitl-sample", UserId = "user-1" });

var userMessage = new Content { Role = "user", Parts = [new Part { Text = "Reimburse $120." }] };

// 2. Execute. The agent will attempt to call the reimburse tool and pause.
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


/// <summary>Reimburse the employee for the given amount.</summary>
/// <param name="amount">Dollar amount to reimburse.</param>
[FunctionTool(Name = "reimburse")]
static object? Reimburse(int amount)
{
    if (amount <= 0)
        return new Dictionary<string, object?> { ["status"] = "Invalid reimbursement amount." };

    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["amount"] = amount
    };
}
```

If `Accepted` is true, the ADK will proceed to invoke the underlying sensitive tool and relay the result to the LLM. If false, it blocks execution and informs the LLM that the user denied the request.