using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Auth;

namespace GoogleAdk.Core.Agents;

/// <summary>
/// Context available to callbacks and tool invocations during an agent run.
/// Provides access to state, event actions, artifacts, and memory.
/// </summary>
public class AgentContext
{
    public InvocationContext InvocationContext { get; }
    public EventActions EventActions { get; }
    public State State { get; }
    public string? FunctionCallId { get; set; }

    public AgentContext(InvocationContext invocationContext, EventActions? eventActions = null, string? functionCallId = null)
    {
        InvocationContext = invocationContext;
        EventActions = eventActions ?? new EventActions();
        State = new State(invocationContext.Session.State, EventActions.StateDelta);
        FunctionCallId = functionCallId;
    }

    public string AppName => InvocationContext.AppName;
    public string UserId => InvocationContext.UserId;
    public Session Session => InvocationContext.Session;

    public void RequestCredential(AuthConfig authConfig)
    {
        if (string.IsNullOrEmpty(FunctionCallId))
            throw new InvalidOperationException("FunctionCallId is not set.");

        var authHandler = new AuthHandler(authConfig);
        EventActions.RequestedAuthConfigs[FunctionCallId] = authHandler.GenerateAuthRequest();
    }

    public AuthCredential? GetAuthResponse(AuthConfig authConfig)
    {
        var authHandler = new AuthHandler(authConfig);
        return authHandler.GetAuthResponse(State);
    }
}
