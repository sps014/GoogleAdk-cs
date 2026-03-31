// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace GoogleAdk.Core.Abstractions.Models;

/// <summary>
/// Represents a part of a content message (text, function call, function response, inline data, etc.).
/// </summary>
public class Part
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("functionCall")]
    public FunctionCall? FunctionCall { get; set; }

    [JsonPropertyName("functionResponse")]
    public FunctionResponse? FunctionResponse { get; set; }

    [JsonPropertyName("inlineData")]
    public InlineData? InlineData { get; set; }

    [JsonPropertyName("codeExecutionResult")]
    public CodeExecutionResult? CodeExecutionResult { get; set; }
}

/// <summary>
/// Represents a function call from the model.
/// </summary>
public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public Dictionary<string, object?>? Args { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

/// <summary>
/// Represents a function response to the model.
/// </summary>
public class FunctionResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public Dictionary<string, object?>? Response { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

/// <summary>
/// Inline binary data (e.g., images).
/// </summary>
public class InlineData
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Result from code execution.
/// </summary>
public class CodeExecutionResult
{
    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    [JsonPropertyName("output")]
    public string? Output { get; set; }
}

/// <summary>
/// Represents content in a conversation (a message from user or model).
/// </summary>
public class Content
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<Part>? Parts { get; set; }
}

/// <summary>
/// A function declaration that a tool exposes to the model.
/// </summary>
public class FunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?>? Parameters { get; set; }
}

/// <summary>
/// A tool declaration sent to the model.
/// </summary>
public class ToolDeclaration
{
    [JsonPropertyName("functionDeclarations")]
    public List<FunctionDeclaration>? FunctionDeclarations { get; set; }

    [JsonPropertyName("googleSearch")]
    public Dictionary<string, object?>? GoogleSearch { get; set; }

    [JsonPropertyName("googleSearchRetrieval")]
    public Dictionary<string, object?>? GoogleSearchRetrieval { get; set; }
}

/// <summary>
/// Configuration for content generation requests.
/// </summary>
public class GenerateContentConfig
{
    [JsonPropertyName("systemInstruction")]
    public string? SystemInstruction { get; set; }

    [JsonPropertyName("tools")]
    public List<ToolDeclaration>? Tools { get; set; }

    [JsonPropertyName("responseSchema")]
    public Dictionary<string, object?>? ResponseSchema { get; set; }

    [JsonPropertyName("responseMimeType")]
    public string? ResponseMimeType { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("topP")]
    public double? TopP { get; set; }

    [JsonPropertyName("topK")]
    public int? TopK { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("candidateCount")]
    public int? CandidateCount { get; set; }

    [JsonPropertyName("stopSequences")]
    public List<string>? StopSequences { get; set; }
}

/// <summary>
/// Usage metadata from a content generation response.
/// </summary>
public class UsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int? PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int? CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int? TotalTokenCount { get; set; }
}

/// <summary>
/// Grounding metadata from the response.
/// </summary>
public class GroundingMetadata
{
    [JsonPropertyName("webSearchQueries")]
    public List<string>? WebSearchQueries { get; set; }

    [JsonPropertyName("searchEntryPoint")]
    public Dictionary<string, object?>? SearchEntryPoint { get; set; }
}

/// <summary>
/// Citation metadata from the response.
/// </summary>
public class CitationMetadata
{
    [JsonPropertyName("citations")]
    public List<Dictionary<string, object?>>? Citations { get; set; }
}

/// <summary>
/// Transcription data for audio.
/// </summary>
public class Transcription
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
