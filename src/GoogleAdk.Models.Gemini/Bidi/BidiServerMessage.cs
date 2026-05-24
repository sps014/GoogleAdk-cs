using System.Text.Json.Serialization;
using GenerativeAI.Types;

namespace GoogleAdk.Models.Gemini.Bidi;

public class BidiServerMessage
{
    [JsonPropertyName("setupComplete")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BidiSetupComplete? SetupComplete { get; set; }

    [JsonPropertyName("serverContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BidiServerContent? ServerContent { get; set; }

    [JsonPropertyName("toolCall")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BidiToolCall? ToolCall { get; set; }

    [JsonPropertyName("toolCallCancellation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BidiToolCallCancellation? ToolCallCancellation { get; set; }
}

public class BidiSetupComplete
{
}

public class BidiServerContent
{
    [JsonPropertyName("modelTurn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BidiModelTurn? ModelTurn { get; set; }

    [JsonPropertyName("turnComplete")]
    public bool TurnComplete { get; set; }

    [JsonPropertyName("interrupted")]
    public bool Interrupted { get; set; }
}

public class BidiModelTurn
{
    [JsonPropertyName("parts")]
    public Part[] Parts { get; set; } = Array.Empty<Part>();
}

public class BidiToolCall
{
    [JsonPropertyName("functionCalls")]
    public FunctionCall[] FunctionCalls { get; set; } = Array.Empty<FunctionCall>();
}

public class BidiToolCallCancellation
{
    [JsonPropertyName("ids")]
    public string[] Ids { get; set; } = Array.Empty<string>();
}