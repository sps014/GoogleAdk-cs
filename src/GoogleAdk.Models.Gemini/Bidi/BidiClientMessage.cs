using System.Text.Json.Serialization;
using GenerativeAI.Types;

namespace GoogleAdk.Models.Gemini.Bidi;

public class BidiClientMessage
{
    [JsonPropertyName("setup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BidiSetup? Setup { get; set; }

    [JsonPropertyName("clientContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BidiClientContent? ClientContent { get; set; }

    [JsonPropertyName("realtimeInput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BidiRealtimeInput? RealtimeInput { get; set; }

    [JsonPropertyName("toolResponse")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BidiToolResponse? ToolResponse { get; set; }
}

public class BidiSetup
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("generationConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GenerationConfig? GenerationConfig { get; set; }

    [JsonPropertyName("systemInstruction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Content? SystemInstruction { get; set; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Tool[]? Tools { get; set; }
}

public class BidiClientContent
{
    [JsonPropertyName("turns")]
    public Content[] Turns { get; set; } = Array.Empty<Content>();

    [JsonPropertyName("turnComplete")]
    public bool TurnComplete { get; set; }
}

public class BidiRealtimeInput
{
    [JsonPropertyName("mediaChunks")]
    public BidiMediaChunk[] MediaChunks { get; set; } = Array.Empty<BidiMediaChunk>();
}

public class BidiMediaChunk
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}

public class BidiToolResponse
{
    [JsonPropertyName("functionResponses")]
    public object[] FunctionResponses { get; set; } = Array.Empty<object>();
}