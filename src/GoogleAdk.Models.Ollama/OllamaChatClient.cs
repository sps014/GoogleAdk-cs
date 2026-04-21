using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GoogleAdk.Models.Ollama;

/// <summary>
/// An IChatClient implementation for Ollama that supports model-native thinking
/// AND tool calling. Calls the Ollama /api/chat endpoint directly — no MEAI
/// Ollama package required. Injects "think": true when the agent's planner has
/// ThinkingConfig.IncludeThoughts = true, emits TextReasoningContent for thought
/// chunks, and fully supports tool declarations and FunctionCallContent round-trips.
///
/// Supports thinking-capable models such as:
///   - gemma4:e4b / gemma4:26b / gemma4:31b
///   - deepseek-r1
///   - qwq
///
/// Thinking is activated automatically when the agent uses a BuiltInPlanner
/// with ThinkingConfig.IncludeThoughts = true — no manual configuration needed.
/// </summary>
public sealed class OllamaChatClient : IChatClient, IDisposable
{
    private readonly Uri _baseUri;
    private readonly string _model;
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <param name="baseUri">Ollama base URI, e.g. http://localhost:11434</param>
    /// <param name="model">Model name, e.g. "gemma4:e4b"</param>
    /// <param name="httpClient">Optional HttpClient to reuse</param>
    public OllamaChatClient(Uri baseUri, string model, HttpClient? httpClient = null)
    {
        _baseUri = baseUri;
        _model = model;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    /// <inheritdoc />
    public ChatClientMetadata Metadata => new(_model, _baseUri);

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? key = null)
        => serviceType.IsInstanceOfType(this) ? this : null;

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, options, stream: false);
        var url = new Uri(_baseUri, "/api/chat");

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        using var httpResp = await _http.PostAsync(url, httpContent, cancellationToken);
        httpResp.EnsureSuccessStatusCode();

        var responseJson = await httpResp.Content.ReadAsStringAsync(cancellationToken);
        var ollResp = JsonSerializer.Deserialize<OllamaResponse>(responseJson, _jsonOptions);
        var contents = ExtractContents(ollResp?.Message);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, options, stream: true);
        var url = new Uri(_baseUri, "/api/chat");

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

        using var resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // Use ReadLineAsync null-check for EOF — avoids CA2024 (EndOfStream is sync)
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break; // EOF
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaResponse? chunk;
            try { chunk = JsonSerializer.Deserialize<OllamaResponse>(line, _jsonOptions); }
            catch (JsonException) { continue; }

            if (chunk?.Message == null) continue;

            // Thinking chunk: emit TextReasoningContent
            if (!string.IsNullOrEmpty(chunk.Message.Thinking))
            {
                yield return new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new TextReasoningContent(chunk.Message.Thinking)],
                };
            }

            // Content chunk: emit TextContent
            if (!string.IsNullOrEmpty(chunk.Message.Content))
            {
                yield return new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent(chunk.Message.Content)],
                };
            }

            // Tool call chunks (Ollama delivers all tool_calls in the done=true chunk)
            if (chunk.Message.ToolCalls is { Count: > 0 })
            {
                var toolContents = BuildFunctionCallContents(chunk.Message.ToolCalls);
                if (toolContents.Count > 0)
                {
                    yield return new ChatResponseUpdate
                    {
                        Role = ChatRole.Assistant,
                        Contents = toolContents,
                    };
                }
            }

            if (chunk.Done == true) break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private List<AIContent> ExtractContents(OllamaMessage? msg)
    {
        var contents = new List<AIContent>();
        if (msg == null) return contents;

        if (!string.IsNullOrEmpty(msg.Thinking))
            contents.Add(new TextReasoningContent(msg.Thinking));
        if (!string.IsNullOrEmpty(msg.Content))
            contents.Add(new TextContent(msg.Content));
        if (msg.ToolCalls is { Count: > 0 })
            contents.AddRange(BuildFunctionCallContents(msg.ToolCalls));

        return contents;
    }

    private static List<AIContent> BuildFunctionCallContents(List<OllamaToolCall> toolCalls)
    {
        var result = new List<AIContent>();
        foreach (var tc in toolCalls)
        {
            var fn = tc.Function;
            if (fn == null) continue;

            IDictionary<string, object?>? args = null;
            if (fn.Arguments.ValueKind != JsonValueKind.Undefined &&
                fn.Arguments.ValueKind != JsonValueKind.Null)
            {
                args = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    fn.Arguments.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            result.Add(new FunctionCallContent(
                callId: fn.Name ?? Guid.NewGuid().ToString("N"),
                name: fn.Name ?? string.Empty,
                arguments: args));
        }
        return result;
    }

    private OllamaRequest BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream)
    {
        var ollamaMessages = new List<OllamaMessage>();

        foreach (var m in messages)
        {
            // FunctionResultContent can arrive as ChatRole.Tool (MEAI convention) or
            // ChatRole.User (ADK/Gemini convention where tool results go in the user turn).
            // Either way, Ollama needs them as role="tool".
            var funcResults = m.Contents.OfType<FunctionResultContent>().ToList();
            if (funcResults.Count > 0)
            {
                foreach (var fc in funcResults)
                {
                    var resultStr = fc.Result is JsonElement je
                        ? je.GetRawText()
                        : JsonSerializer.Serialize(fc.Result);
                    ollamaMessages.Add(new OllamaMessage
                    {
                        Role = "tool",
                        Content = resultStr,
                    });
                }
                // If there was also text on this message, fall through and add it separately.
                var extraText = string.Concat(m.Contents.OfType<TextContent>().Select(t => t.Text));
                if (!string.IsNullOrEmpty(extraText))
                {
                    var r = m.Role == ChatRole.System ? "system"
                          : m.Role == ChatRole.Assistant ? "assistant"
                          : "user";
                    ollamaMessages.Add(new OllamaMessage { Role = r, Content = extraText });
                }
                continue;
            }

            if (m.Role == ChatRole.Tool)
            {
                // Fallback: pure tool-role message with no FunctionResultContent (shouldn't happen, but safe)
                ollamaMessages.Add(new OllamaMessage
                {
                    Role = "tool",
                    Content = string.Concat(m.Contents.OfType<TextContent>().Select(t => t.Text)),
                });
                continue;
            }

            var role = m.Role == ChatRole.System ? "system"
                     : m.Role == ChatRole.Assistant ? "assistant"
                     : "user";

            var textParts = m.Contents.OfType<TextContent>().ToList();
            var functionCalls = m.Contents.OfType<FunctionCallContent>().ToList();

            if (functionCalls.Count > 0)
            {
                // Assistant message with tool_calls
                var toolCalls = functionCalls.Select(fc =>
                {
                    JsonElement argsElement;
                    if (fc.Arguments != null)
                    {
                        var argsJson = JsonSerializer.Serialize(fc.Arguments);
                        argsElement = JsonDocument.Parse(argsJson).RootElement.Clone();
                    }
                    else
                    {
                        argsElement = JsonDocument.Parse("{}").RootElement.Clone();
                    }

                    return new OllamaToolCall
                    {
                        Function = new OllamaToolCallFunction
                        {
                            Name = fc.Name,
                            Arguments = argsElement,
                        }
                    };
                }).ToList();

                var textContent = string.Concat(textParts.Select(t => t.Text));
                ollamaMessages.Add(new OllamaMessage
                {
                    Role = "assistant",
                    // Send null (omitted) rather than "" so models that inspect content
                    // alongside tool_calls aren't confused by an empty string.
                    Content = textContent.Length > 0 ? textContent : null,
                    ToolCalls = toolCalls,
                });
            }
            else
            {
                var msgText = string.Concat(m.Contents.OfType<TextContent>().Select(t => t.Text));
                ollamaMessages.Add(new OllamaMessage
                {
                    Role = role,
                    Content = msgText.Length > 0 ? msgText : null,
                });
            }
        }

        // Build tool declarations from ChatOptions.Tools
        List<OllamaToolDeclaration>? tools = null;
        if (options?.Tools is { Count: > 0 })
        {
            tools = [];
            foreach (var tool in options.Tools.OfType<AIFunction>())
            {
                var schemaJson = tool.JsonSchema.GetRawText();
                var schemaNode = JsonNode.Parse(schemaJson);
                tools.Add(new OllamaToolDeclaration
                {
                    Type = "function",
                    Function = new OllamaFunctionDeclaration
                    {
                        Name = tool.Name,
                        Description = tool.Description ?? string.Empty,
                        Parameters = schemaNode,
                    }
                });
            }
        }

        // Thinking is opt-in: MeaiLlm sets AdditionalProperties["think"] = true
        // when the agent uses BuiltInPlanner with ThinkingConfig.IncludeThoughts = true.
        bool think = options?.AdditionalProperties?.TryGetValue("think", out var thinkVal) == true
            && thinkVal is true;

        return new OllamaRequest
        {
            Model = _model,
            Messages = ollamaMessages,
            Stream = stream,
            Think = think ? true : null,
            Tools = tools,
            Options = options?.Temperature is { } temp
                ? new OllamaModelOptions { Temperature = (float)temp }
                : null,
        };
    }

    // ── Wire models ───────────────────────────────────────────────────────────

    private sealed class OllamaRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OllamaMessage> Messages { get; set; } = [];
        public bool Stream { get; set; }
        [JsonPropertyName("think")]
        public bool? Think { get; set; }
        public List<OllamaToolDeclaration>? Tools { get; set; }
        public OllamaModelOptions? Options { get; set; }
    }

    private sealed class OllamaMessage
    {
        public string Role { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? Thinking { get; set; }
        public List<OllamaToolCall>? ToolCalls { get; set; }
    }

    private sealed class OllamaToolCall
    {
        public OllamaToolCallFunction? Function { get; set; }
    }

    private sealed class OllamaToolCallFunction
    {
        public string? Name { get; set; }
        public JsonElement Arguments { get; set; }
    }

    private sealed class OllamaToolDeclaration
    {
        public string Type { get; set; } = "function";
        public OllamaFunctionDeclaration? Function { get; set; }
    }

    private sealed class OllamaFunctionDeclaration
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public JsonNode? Parameters { get; set; }
    }

    private sealed class OllamaModelOptions
    {
        public float? Temperature { get; set; }
    }

    private sealed class OllamaResponse
    {
        public string? Model { get; set; }
        public OllamaMessage? Message { get; set; }
        public bool? Done { get; set; }
    }
}
