using GoogleAdk.Core.Abstractions.Auth;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Memory;
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

    /// <summary>
    /// Adds a UI widget to be rendered by the client.
    /// </summary>
    public void RenderUiWidget(UiWidget widget)
    {
        if (EventActions.RenderUiWidgets.Any(w => w.Id == widget.Id))
            return;
        EventActions.RenderUiWidgets.Add(widget);
    }

    /// <summary>
    /// Convenience method to save an artifact and automatically update the artifact delta.
    /// </summary>
    public async Task<int> SaveArtifactAsync(string fileName, GoogleAdk.Core.Abstractions.Models.Part artifact)
    {
        if (InvocationContext.ArtifactService == null)
            throw new InvalidOperationException("ArtifactService is not configured.");

        var req = new GoogleAdk.Core.Abstractions.Artifacts.SaveArtifactRequest
        {
            AppName = AppName,
            UserId = UserId,
            SessionId = Session.Id,
            Filename = fileName,
            Artifact = artifact
        };

        var version = await InvocationContext.ArtifactService.SaveArtifactAsync(req);
        EventActions.ArtifactDelta[fileName] = version;
        return version;
    }

    /// <summary>
    /// Convenience method to load an artifact.
    /// </summary>
    public async Task<GoogleAdk.Core.Abstractions.Models.Part?> LoadArtifactAsync(string fileName, int? version = null)
    {
        if (InvocationContext.ArtifactService == null)
            throw new InvalidOperationException("ArtifactService is not configured.");

        var req = new GoogleAdk.Core.Abstractions.Artifacts.LoadArtifactRequest
        {
            AppName = AppName,
            UserId = UserId,
            SessionId = Session.Id,
            Filename = fileName,
            Version = version
        };

        return await InvocationContext.ArtifactService.LoadArtifactAsync(req);
    }

    /// <summary>
    /// Lists all artifact keys (filenames) for the current user and session.
    /// </summary>
    public async Task<List<string>> ListArtifactsAsync()
    {
        if (InvocationContext.ArtifactService == null)
            throw new InvalidOperationException("ArtifactService is not configured.");

        var req = new GoogleAdk.Core.Abstractions.Artifacts.ListArtifactKeysRequest
        {
            AppName = AppName,
            UserId = UserId,
            SessionId = Session.Id
        };

        return await InvocationContext.ArtifactService.ListArtifactKeysAsync(req);
    }

}
