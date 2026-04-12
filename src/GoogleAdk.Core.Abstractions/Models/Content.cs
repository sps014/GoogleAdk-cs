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
    public Schema? Parameters { get; set; }
}

/// <summary>
/// The Schema object allows the definition of input and output data types.
/// </summary>
public class Schema
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, Schema>? Properties { get; set; }

    [JsonPropertyName("items")]
    public Schema? Items { get; set; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }

    public static implicit operator Schema?(Dictionary<string, object?>? dict)
    {
        if (dict == null) return null;
        var json = System.Text.Json.JsonSerializer.Serialize(dict);
        return System.Text.Json.JsonSerializer.Deserialize<Schema>(json);
    }
}

/// <summary>
/// A tool declaration sent to the model.
/// </summary>
public class ToolDeclaration
{
    [JsonPropertyName("functionDeclarations")]
    public List<FunctionDeclaration>? FunctionDeclarations { get; set; }

    [JsonPropertyName("googleSearch")]
    public GoogleSearchConfig? GoogleSearch { get; set; }

    [JsonPropertyName("googleSearchRetrieval")]
    public GoogleSearchRetrievalConfig? GoogleSearchRetrieval { get; set; }

    [JsonPropertyName("retrieval")]
    public RetrievalConfig? Retrieval { get; set; }

    [JsonPropertyName("urlContext")]
    public UrlContextConfig? UrlContext { get; set; }

    [JsonPropertyName("enterpriseWebSearch")]
    public EnterpriseWebSearchConfig? EnterpriseWebSearch { get; set; }

    [JsonPropertyName("googleMaps")]
    public GoogleMapsConfig? GoogleMaps { get; set; }

    [JsonPropertyName("codeExecution")]
    public CodeExecutionConfig? CodeExecution { get; set; }
}

public class CodeExecutionConfig
{
}

public class GoogleSearchConfig
{
}

public class GoogleSearchRetrievalConfig
{
    [JsonPropertyName("dynamicRetrievalConfig")]
    public DynamicRetrievalConfig? DynamicRetrievalConfig { get; set; }
}

public class DynamicRetrievalConfig
{
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("dynamicThreshold")]
    public double? DynamicThreshold { get; set; }
}

public class UrlContextConfig
{
}

public class EnterpriseWebSearchConfig
{
}

public class GoogleMapsConfig
{
}

/// <summary>
/// Configuration for retrieval tools (like Vertex AI Search).
/// </summary>
public class RetrievalConfig
{
    [JsonPropertyName("vertexAiSearch")]
    public VertexAiSearchConfig? VertexAiSearch { get; set; }

    [JsonPropertyName("vertexRagStore")]
    public VertexRagStoreConfig? VertexRagStore { get; set; }
}

/// <summary>
/// Configuration for Vertex RAG Store.
/// </summary>
public class VertexRagStoreConfig
{
    [JsonPropertyName("ragCorpora")]
    public List<string>? RagCorpora { get; set; }

    [JsonPropertyName("ragResources")]
    public List<VertexAiSearchDataStoreSpec>? RagResources { get; set; }

    [JsonPropertyName("similarityTopK")]
    public int? SimilarityTopK { get; set; }

    [JsonPropertyName("vectorDistanceThreshold")]
    public double? VectorDistanceThreshold { get; set; }
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
    public ThinkingConfig? ThinkingConfig { get; set; }

    [JsonPropertyName("safetySettings")]
    public List<SafetySetting>? SafetySettings { get; set; }

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

    [JsonPropertyName("responseModalities")]
    public List<Modality>? ResponseModalities { get; set; }

    [JsonPropertyName("speechConfig")]
    public SpeechConfig? SpeechConfig { get; set; }
}

/// <summary>
/// Configuration for model thinking features.
/// </summary>
public class ThinkingConfig
{
    [JsonPropertyName("thinkingBudget")]
    public int? ThinkingBudget { get; set; }

    [JsonPropertyName("includeThoughts")]
    public bool? IncludeThoughts { get; set; }
}

/// <summary>
/// Safety setting, affecting the safety-related filters.
/// </summary>
public class SafetySetting
{
    [JsonPropertyName("category")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HarmCategory Category { get; set; }

    [JsonPropertyName("threshold")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HarmBlockThreshold Threshold { get; set; }
}

/// <summary>
/// The category of a rating.
/// </summary>
public enum HarmCategory
{
    HARM_CATEGORY_UNSPECIFIED,
    HARM_CATEGORY_HATE_SPEECH,
    HARM_CATEGORY_DANGEROUS_CONTENT,
    HARM_CATEGORY_HARASSMENT,
    HARM_CATEGORY_SEXUALLY_EXPLICIT
}

/// <summary>
/// Block at and beyond a specified harm probability.
/// </summary>
public enum HarmBlockThreshold
{
    HARM_BLOCK_THRESHOLD_UNSPECIFIED,
    BLOCK_LOW_AND_ABOVE,
    BLOCK_MEDIUM_AND_ABOVE,
    BLOCK_ONLY_HIGH,
    BLOCK_NONE,
    OFF
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
    public SearchEntryPoint? SearchEntryPoint { get; set; }

    [JsonPropertyName("groundingChunks")]
    public List<GroundingChunk>? GroundingChunks { get; set; }

    [JsonPropertyName("groundingSupports")]
    public List<GroundingSupport>? GroundingSupports { get; set; }
}

public class SearchEntryPoint
{
    [JsonPropertyName("renderedContent")]
    public string? RenderedContent { get; set; }
}

public class GroundingChunk
{
    [JsonPropertyName("web")]
    public WebGroundingChunk? Web { get; set; }

    [JsonPropertyName("retrievedContext")]
    public RetrievedContextGroundingChunk? RetrievedContext { get; set; }
}

public class WebGroundingChunk
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

public class RetrievedContextGroundingChunk
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

public class GroundingSupport
{
    [JsonPropertyName("segment")]
    public Segment? Segment { get; set; }

    [JsonPropertyName("groundingChunkIndices")]
    public List<int>? GroundingChunkIndices { get; set; }
}

public class Segment
{
    [JsonPropertyName("startIndex")]
    public int? StartIndex { get; set; }

    [JsonPropertyName("endIndex")]
    public int? EndIndex { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>
/// Citation metadata from the response.
/// </summary>
public class CitationMetadata
{
    [JsonPropertyName("citations")]
    public List<Citation>? Citations { get; set; }
}

public class Citation
{
    [JsonPropertyName("startIndex")]
    public int? StartIndex { get; set; }

    [JsonPropertyName("endIndex")]
    public int? EndIndex { get; set; }

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("license")]
    public string? License { get; set; }

    [JsonPropertyName("publicationDate")]
    public DateInfo? PublicationDate { get; set; }
}

public class DateInfo
{
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("month")]
    public int? Month { get; set; }

    [JsonPropertyName("day")]
    public int? Day { get; set; }
}

/// <summary>
/// Transcription data for audio.
/// </summary>
public class Transcription
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Modality
{
    MODALITY_UNSPECIFIED,
    TEXT,
    IMAGE,
    AUDIO,
    VIDEO
}

/// <summary>
/// Speech configuration for live agents.
/// </summary>
public class SpeechConfig
{
    [JsonPropertyName("voiceConfig")]
    public VoiceConfig? VoiceConfig { get; set; }
}

public class VoiceConfig
{
    [JsonPropertyName("prebuiltVoiceConfig")]
    public PrebuiltVoiceConfig? PrebuiltVoiceConfig { get; set; }
}

public class PrebuiltVoiceConfig
{
    [JsonPropertyName("voiceName")]
    public string? VoiceName { get; set; }
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
/// Determine whether start of speech event interrupts the model's response.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActivityHandling
{
    ACTIVITY_HANDLING_UNSPECIFIED,
    START_OF_ACTIVITY_INTERRUPTS,
    NO_INTERRUPTION
}

/// <summary>
/// Define which input is included in the user's turn.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TurnCoverage
{
    TURN_COVERAGE_UNSPECIFIED,
    TURN_INCLUDES_ONLY_ACTIVITY,
    TURN_INCLUDES_ALL_INPUT
}

/// <summary>
/// Sensitivity of start of speech detection.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StartSensitivity
{
    START_SENSITIVITY_UNSPECIFIED,
    START_SENSITIVITY_HIGH,
    START_SENSITIVITY_LOW
}

/// <summary>
/// Sensitivity of end of speech detection.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EndSensitivity
{
    END_SENSITIVITY_UNSPECIFIED,
    END_SENSITIVITY_HIGH,
    END_SENSITIVITY_LOW
}

/// <summary>
/// Settings for automatic voice activity detection.
/// </summary>
public class AutomaticActivityDetection
{
    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }

    [JsonPropertyName("startOfSpeechSensitivity")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StartSensitivity? StartOfSpeechSensitivity { get; set; }

    [JsonPropertyName("endOfSpeechSensitivity")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EndSensitivity? EndOfSpeechSensitivity { get; set; }

    [JsonPropertyName("prefixPaddingMs")]
    public int? PrefixPaddingMs { get; set; }

    [JsonPropertyName("silenceDurationMs")]
    public int? SilenceDurationMs { get; set; }
}

/// <summary>
/// Sliding window parameters.
/// </summary>
public class SlidingWindow
{
    [JsonPropertyName("targetTokens")]
    public int? TargetTokens { get; set; }
}

/// <summary>
/// Realtime input configuration.
/// </summary>
public class RealtimeInputConfig
{
    [JsonPropertyName("automaticActivityDetection")]
    public AutomaticActivityDetection? AutomaticActivityDetection { get; set; }

    [JsonPropertyName("activityHandling")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActivityHandling? ActivityHandling { get; set; }

    [JsonPropertyName("turnCoverage")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TurnCoverage? TurnCoverage { get; set; }
}

/// <summary>
/// Proactivity configuration.
/// </summary>
public class ProactivityConfig
{
    [JsonPropertyName("proactiveAudio")]
    public bool? ProactiveAudio { get; set; }
}

/// <summary>
/// Session resumption configuration.
/// </summary>
public class SessionResumptionConfig
{
    [JsonPropertyName("handle")]
    public string? Handle { get; set; }

    [JsonPropertyName("transparent")]
    public bool? Transparent { get; set; }
}

/// <summary>
/// Context window compression configuration.
/// </summary>
public class ContextWindowCompressionConfig
{
    [JsonPropertyName("triggerTokens")]
    public int? TriggerTokens { get; set; }

    [JsonPropertyName("slidingWindow")]
    public SlidingWindow? SlidingWindow { get; set; }
}
