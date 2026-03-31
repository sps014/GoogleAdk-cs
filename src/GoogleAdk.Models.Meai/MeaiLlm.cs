// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace GoogleAdk.Models.Meai;

/// <summary>
/// An LLM implementation that wraps any Microsoft.Extensions.AI IChatClient.
/// This enables using any MEAI-compatible provider (OpenAI, Anthropic, Ollama,
/// Azure OpenAI, etc.) as a BaseLlm in the ADK agent system.
/// </summary>
public class MeaiLlm : BaseLlm
{
    private readonly IChatClient _chatClient;

    /// <summary>
    /// Creates a new MeaiLlm wrapping the given IChatClient.
    /// </summary>
    /// <param name="model">The model name (e.g., "gpt-4o", "claude-3-opus").</param>
    /// <param name="chatClient">The MEAI chat client to use.</param>
    public MeaiLlm(string model, IChatClient chatClient) : base(model)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<LlmResponse> GenerateContentAsync(
        LlmRequest llmRequest,
        bool stream = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = ConvertToMeaiMessages(llmRequest);
        var options = ConvertToMeaiOptions(llmRequest);

        if (stream)
        {
            var textBuffer = string.Empty;
            await foreach (var update in _chatClient.GetStreamingResponseAsync(
                messages, options, cancellationToken))
            {
                if (update.Text != null)
                {
                    textBuffer += update.Text;
                    yield return new LlmResponse
                    {
                        Content = new Content
                        {
                            Role = "model",
                            Parts = new List<Part> { new Part { Text = update.Text } }
                        },
                        Partial = true,
                    };
                }

                // Check for tool calls in the streaming update
                if (update.Contents != null)
                {
                    foreach (var content in update.Contents)
                    {
                        if (content is FunctionCallContent functionCall)
                        {
                            yield return new LlmResponse
                            {
                                Content = new Content
                                {
                                    Role = "model",
                                    Parts = new List<Part>
                                    {
                                        new Part
                                        {
                                            FunctionCall = new FunctionCall
                                            {
                                                Name = functionCall.Name,
                                                Args = ConvertArgsToDictionary(functionCall.Arguments),
                                                Id = functionCall.CallId,
                                            }
                                        }
                                    }
                                },
                                TurnComplete = true,
                            };
                        }
                    }
                }
            }
        }
        else
        {
            var response = await _chatClient.GetResponseAsync(messages, options, cancellationToken);
            yield return ConvertFromMeaiResponse(response);
        }
    }

    /// <inheritdoc />
    public override Task<BaseLlmConnection> ConnectAsync(LlmRequest llmRequest)
    {
        throw new NotSupportedException(
            "Live/bidirectional connections are not supported through MEAI IChatClient. " +
            "Use a native provider implementation for live connections.");
    }

    /// <summary>
    /// Converts an ADK LlmRequest to MEAI ChatMessage list.
    /// </summary>
    private static List<ChatMessage> ConvertToMeaiMessages(LlmRequest llmRequest)
    {
        var messages = new List<ChatMessage>();

        // Add system instruction
        if (llmRequest.Config?.SystemInstruction != null)
        {
            messages.Add(new ChatMessage(ChatRole.System, llmRequest.Config.SystemInstruction));
        }

        // Convert contents
        foreach (var content in llmRequest.Contents)
        {
            var role = content.Role?.ToLowerInvariant() switch
            {
                "user" => ChatRole.User,
                "model" => ChatRole.Assistant,
                "system" => ChatRole.System,
                "tool" => ChatRole.Tool,
                _ => ChatRole.User,
            };

            var aiContents = new List<AIContent>();
            if (content.Parts != null)
            {
                foreach (var part in content.Parts)
                {
                    if (part.Text != null)
                    {
                        aiContents.Add(new TextContent(part.Text));
                    }
                    else if (part.FunctionCall != null)
                    {
                        aiContents.Add(new FunctionCallContent(
                            part.FunctionCall.Id ?? string.Empty,
                            part.FunctionCall.Name ?? string.Empty,
                            part.FunctionCall.Args != null
                                ? new Dictionary<string, object?>(part.FunctionCall.Args)
                                : null));
                    }
                    else if (part.FunctionResponse != null)
                    {
                        // Serialize to JsonElement so the downstream GenerativeAI library
                        // can call .AsObject() on it without type-mismatch errors.
                        var responseDict = part.FunctionResponse.Response ?? new Dictionary<string, object?>();
                        var jsonResult = JsonSerializer.Deserialize<JsonElement>(
                            JsonSerializer.Serialize(responseDict));
                        aiContents.Add(new FunctionResultContent(
                            part.FunctionResponse.Id ?? string.Empty,
                            jsonResult));
                    }
                    else if (part.InlineData != null)
                    {
                        aiContents.Add(new DataContent(
                            Convert.FromBase64String(part.InlineData.Data ?? string.Empty),
                            part.InlineData.MimeType ?? "application/octet-stream"));
                    }
                }
            }

            messages.Add(new ChatMessage(role, aiContents));
        }

        return messages;
    }

    /// <summary>
    /// Converts ADK config to MEAI ChatOptions.
    /// </summary>
    private ChatOptions? ConvertToMeaiOptions(LlmRequest llmRequest)
    {
        var config = llmRequest.Config;
        if (config == null) return new ChatOptions { ModelId = Model };

        var options = new ChatOptions
        {
            ModelId = llmRequest.Model ?? Model,
            Temperature = (float?)config.Temperature,
            MaxOutputTokens = config.MaxOutputTokens,
            TopP = (float?)config.TopP,
            TopK = config.TopK,
            StopSequences = config.StopSequences,
        };

        // Convert tool declarations to MEAI tools
        if (llmRequest.ToolsDict.Count > 0)
        {
            options.Tools = new List<AITool>();
            foreach (var kvp in llmRequest.ToolsDict)
            {
                var declaration = kvp.Value.GetDeclaration();
                if (declaration != null)
                {
                    var name = declaration.Name ?? kvp.Key;
                    var description = declaration.Description ?? string.Empty;

                    // Convert our Dictionary-based schema to a JsonElement for MEAI.
                    // Sanitize to ensure all 'type: object' nodes have 'properties'
                    // (required by the Gemini API's structured output validation).
                    JsonElement schemaElement = default;
                    if (declaration.Parameters != null)
                    {
                        var schemaJson = JsonSerializer.Serialize(declaration.Parameters);
                        using var doc = JsonDocument.Parse(schemaJson);
                        var sanitized = SanitizeObjectSchema(doc.RootElement);
                        schemaElement = JsonDocument.Parse(sanitized.GetRawText()).RootElement.Clone();
                    }

                    var aiDeclaration = AIFunctionFactory.CreateDeclaration(name, description, schemaElement);
                    options.Tools.Add(aiDeclaration);
                }
            }
        }

        return options;
    }

    /// <summary>
    /// Converts an MEAI ChatResponse to an ADK LlmResponse.
    /// </summary>
    private static LlmResponse ConvertFromMeaiResponse(ChatResponse response)
    {
        var parts = new List<Part>();

        var lastMessage = response.Messages.LastOrDefault();
        if (lastMessage?.Contents != null)
        {
            foreach (var content in lastMessage.Contents)
            {
                switch (content)
                {
                    case TextContent textContent:
                        parts.Add(new Part { Text = textContent.Text });
                        break;
                    case FunctionCallContent functionCall:
                        parts.Add(new Part
                        {
                            FunctionCall = new FunctionCall
                            {
                                Name = functionCall.Name,
                                Args = ConvertArgsToDictionary(functionCall.Arguments),
                                Id = functionCall.CallId,
                            }
                        });
                        break;
                }
            }
        }

        var llmResponse = new LlmResponse
        {
            Content = new Content
            {
                Role = "model",
                Parts = parts
            },
            TurnComplete = true,
        };

        // Map usage
        if (response.Usage != null)
        {
            llmResponse.UsageMetadata = new UsageMetadata
            {
                PromptTokenCount = (int)(response.Usage.InputTokenCount ?? 0),
                CandidatesTokenCount = (int)(response.Usage.OutputTokenCount ?? 0),
                TotalTokenCount = (int)(response.Usage.TotalTokenCount ?? 0),
            };
        }

        // Map finish reason
        if (response.FinishReason != null)
        {
            var reason = response.FinishReason.Value;
            if (reason == ChatFinishReason.Stop)
                llmResponse.FinishReason = "STOP";
            else if (reason == ChatFinishReason.Length)
                llmResponse.FinishReason = "MAX_TOKENS";
            else if (reason == ChatFinishReason.ContentFilter)
                llmResponse.FinishReason = "SAFETY";
            else if (reason == ChatFinishReason.ToolCalls)
                llmResponse.FinishReason = "TOOL_CALLS";
            else
                llmResponse.FinishReason = reason.Value;
        }

        return llmResponse;
    }

    private static Dictionary<string, object?>? ConvertArgsToDictionary(IDictionary<string, object?>? args)
    {
        if (args == null) return null;
        return new Dictionary<string, object?>(args);
    }

    /// <summary>
    /// Recursively sanitizes a JSON Schema element to ensure every node with
    /// "type": "object" also has a "properties" key. The Gemini API requires this.
    /// </summary>
    private static JsonElement SanitizeObjectSchema(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return element;

        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            bool hasType = false;
            bool hasProperties = false;
            string? typeValue = null;

            // First pass: detect type and properties
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name == "type" && prop.Value.ValueKind == JsonValueKind.String)
                {
                    hasType = true;
                    typeValue = prop.Value.GetString();
                }
                if (prop.Name == "properties") hasProperties = true;
            }

            // Second pass: write all properties, recursively sanitizing nested schemas
            foreach (var prop in element.EnumerateObject())
            {
                writer.WritePropertyName(prop.Name);

                if (prop.Name == "properties" && prop.Value.ValueKind == JsonValueKind.Object)
                {
                    // Recurse into each property schema
                    writer.WriteStartObject();
                    foreach (var subProp in prop.Value.EnumerateObject())
                    {
                        writer.WritePropertyName(subProp.Name);
                        var sanitized = SanitizeObjectSchema(subProp.Value);
                        sanitized.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                else if (prop.Name == "items" && prop.Value.ValueKind == JsonValueKind.Object)
                {
                    var sanitized = SanitizeObjectSchema(prop.Value);
                    sanitized.WriteTo(writer);
                }
                else
                {
                    prop.Value.WriteTo(writer);
                }
            }

            // If type is "object" but no "properties" key exists, add empty properties
            if (hasType && typeValue == "object" && !hasProperties)
            {
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }
}
