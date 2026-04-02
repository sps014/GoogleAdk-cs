using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using System.Runtime.CompilerServices;

namespace GoogleAdk.E2e.Tests;

/// <summary>
/// A mock LLM that returns pre-configured responses.
/// Supports both streaming (multiple partial + final) and unary (single) modes.
/// Each call to GenerateContentAsync advances to the next response group.
/// </summary>
public class MockLlm : BaseLlm
{
    private readonly List<List<LlmResponse>> _responseGroups;
    private int _callIndex = -1;
    public List<LlmRequest> CapturedRequests { get; } = new();

    public MockLlm(List<List<LlmResponse>> responseGroups) : base("mock-model")
    {
        _responseGroups = responseGroups;
    }

    /// <summary>
    /// Create a MockLlm that returns one response per call (unary style).
    /// </summary>
    public static MockLlm FromResponses(params LlmResponse[] responses)
    {
        var groups = responses.Select(r => new List<LlmResponse> { r }).ToList();
        return new MockLlm(groups);
    }

    /// <summary>
    /// Create a MockLlm from explicit response groups.
    /// Each group is yielded for one GenerateContentAsync call.
    /// Within a group, items are yielded sequentially (simulating streaming chunks).
    /// </summary>
    public static MockLlm FromGroups(params List<LlmResponse>[] groups)
    {
        return new MockLlm(groups.ToList());
    }

    public override async IAsyncEnumerable<LlmResponse> GenerateContentAsync(
        LlmRequest llmRequest,
        bool stream = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CapturedRequests.Add(llmRequest);
        _callIndex++;

        if (_callIndex >= _responseGroups.Count)
            throw new InvalidOperationException(
                $"MockLlm: no more response groups. Call #{_callIndex + 1}, but only {_responseGroups.Count} groups configured.");

        foreach (var response in _responseGroups[_callIndex])
        {
            await Task.Yield(); // simulate async
            yield return response;
        }
    }

    public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
    {
        throw new NotSupportedException("Use GenerateContentAsync for these tests.");
    }
}
