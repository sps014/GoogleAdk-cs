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

    [JsonPropertyName("fileData")]
    public FileData? FileData { get; set; }

    [JsonPropertyName("codeExecutionResult")]
    public CodeExecutionResult? CodeExecutionResult { get; set; }

    [JsonPropertyName("executableCode")]
    public ExecutableCode? ExecutableCode { get; set; }

    [JsonPropertyName("thought")]
    public bool? Thought { get; set; }
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

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

/// <summary>
/// File reference data (URI-based).
/// </summary>
public class FileData
{
    [JsonPropertyName("fileUri")]
    public string? FileUri { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Executable code content.
/// </summary>
public class ExecutableCode
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
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

    [JsonPropertyName("retrieval")]
    public RetrievalConfig? Retrieval { get; set; }

    [JsonPropertyName("urlContext")]
    public Dictionary<string, object?>? UrlContext { get; set; }

    [JsonPropertyName("enterpriseWebSearch")]
    public Dictionary<string, object?>? EnterpriseWebSearch { get; set; }

    [JsonPropertyName("googleMaps")]
    public Dictionary<string, object?>? GoogleMaps { get; set; }
}

/// <summary>
/// Configuration for retrieval tools (like Vertex AI Search).
/// </summary>
public class RetrievalConfig
{
    [JsonPropertyName("vertexAiSearch")]
    public VertexAiSearchConfig? VertexAiSearch { get; set; }
}

/// <summary>
/// Configuration for Vertex AI Search retrieval.
/// </summary>
public class VertexAiSearchConfig
{
    [JsonPropertyName("datastore")]
    public string? Datastore { get; set; }

    [JsonPropertyName("engine")]
    public string? Engine { get; set; }

    [JsonPropertyName("filter")]
    public string? Filter { get; set; }

    [JsonPropertyName("maxResults")]
    public int? MaxResults { get; set; }

    [JsonPropertyName("dataStoreSpecs")]
    public List<VertexAiSearchDataStoreSpec>? DataStoreSpecs { get; set; }
}

/// <summary>
/// A data store specification for Vertex AI Search.
/// </summary>
public class VertexAiSearchDataStoreSpec
{
    [JsonPropertyName("dataStore")]
    public string? DataStore { get; set; }
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

    [JsonPropertyName("thinkingConfig")]
    public Dictionary<string, object?>? ThinkingConfig { get; set; }

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

    [JsonPropertyName("groundingChunks")]
    public List<Dictionary<string, object?>>? GroundingChunks { get; set; }

    [JsonPropertyName("groundingSupports")]
    public List<Dictionary<string, object?>>? GroundingSupports { get; set; }
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

/// <summary>
/// Speech configuration for live agents.
/// </summary>
public class SpeechConfig
{
    [JsonPropertyName("voiceConfig")]
    public Dictionary<string, object?>? VoiceConfig { get; set; }
}

/// <summary>
/// Audio transcription configuration.
/// </summary>
public class AudioTranscriptionConfig
{
    [JsonPropertyName("languageCodes")]
    public List<string>? LanguageCodes { get; set; }
}

/// <summary>
/// Realtime input configuration.
/// </summary>
public class RealtimeInputConfig
{
}

/// <summary>
/// Proactivity configuration.
/// </summary>
public class ProactivityConfig
{
}

/// <summary>
/// Session resumption configuration.
/// </summary>
public class SessionResumptionConfig
{
}

/// <summary>
/// Context window compression configuration.
/// </summary>
public class ContextWindowCompressionConfig
{
}
