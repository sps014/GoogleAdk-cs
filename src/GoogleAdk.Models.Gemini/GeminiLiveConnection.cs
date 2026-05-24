using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using GenerativeAI.Types;
using Google.Apis.Auth.OAuth2;
using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Models.Gemini.Bidi;
using AdkContent = GoogleAdk.Core.Abstractions.Models.Content;
using AdkPart = GoogleAdk.Core.Abstractions.Models.Part;
using AdkFunctionCall = GoogleAdk.Core.Abstractions.Models.FunctionCall;

namespace GoogleAdk.Models.Gemini;

/// <summary>
/// Gemini live connection wrapper using true WebSocket BidiGenerateContent API.
/// </summary>
public sealed class GeminiLiveConnection : BaseLlmConnection
{
    private readonly GeminiLlm _llm;
    private readonly LlmRequest _request;
    private readonly ClientWebSocket _ws;
    private readonly Channel<LlmResponse> _receiveChannel;
    private readonly CancellationTokenSource _cts;
    private Task? _receiveLoopTask;
    private bool _setupComplete;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GeminiLiveConnection(GeminiLlm llm, LlmRequest request)
    {
        _llm = llm;
        _request = request;
        _ws = new ClientWebSocket();
        _receiveChannel = Channel.CreateUnbounded<LlmResponse>();
        _cts = new CancellationTokenSource();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var useVertexAi = string.Equals(
            System.Environment.GetEnvironmentVariable("GOOGLE_GENAI_USE_VERTEXAI"),
            "True", StringComparison.OrdinalIgnoreCase);

        Uri uri;
        if (useVertexAi)
        {
            var location = System.Environment.GetEnvironmentVariable("GOOGLE_CLOUD_LOCATION") ?? "us-central1";
            uri = new Uri($"wss://{location}-aiplatform.googleapis.com/ws/google.cloud.aiplatform.v1beta1.LlmBidiService/BidiGenerateContent");
            
            var credential = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
            var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
            _ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");
        }
        else
        {
            var apiKey = System.Environment.GetEnvironmentVariable("GOOGLE_API_KEY") 
                ?? throw new InvalidOperationException("GOOGLE_API_KEY is required.");
            uri = new Uri($"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={apiKey}");
        }

        await _ws.ConnectAsync(uri, cancellationToken);

        // Start receive loop
        _receiveLoopTask = ReceiveLoopAsync(_cts.Token);

        // Send Setup
        await SendSetupAsync(cancellationToken);
    }

    private async Task SendSetupAsync(CancellationToken cancellationToken)
    {
        var modelName = _request.Model ?? string.Empty;
        
        var useVertexAi = string.Equals(
            System.Environment.GetEnvironmentVariable("GOOGLE_GENAI_USE_VERTEXAI"),
            "True", StringComparison.OrdinalIgnoreCase);

        if (useVertexAi && !modelName.StartsWith("projects/") && !modelName.StartsWith("publishers/"))
        {
            var projectId = System.Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
            var location = System.Environment.GetEnvironmentVariable("GOOGLE_CLOUD_LOCATION") ?? "us-central1";
            if (!string.IsNullOrEmpty(projectId))
            {
                modelName = $"projects/{projectId}/locations/{location}/publishers/google/models/{modelName}";
            }
        }

        var setup = new BidiSetup
        {
            Model = modelName
        };

        if (_request.Config != null)
        {
            var genConfig = new GenerativeAI.Types.GenerationConfig();
            if (_request.Config.Temperature.HasValue)
                genConfig.Temperature = _request.Config.Temperature.Value;
            if (_request.Config.TopP.HasValue)
                genConfig.TopP = _request.Config.TopP.Value;
            if (_request.Config.TopK.HasValue)
                genConfig.TopK = _request.Config.TopK.Value;
            if (_request.Config.MaxOutputTokens.HasValue)
                genConfig.MaxOutputTokens = _request.Config.MaxOutputTokens.Value;
            if (_request.Config.StopSequences?.Count > 0)
                genConfig.StopSequences = _request.Config.StopSequences;

            if (_request.Config.ResponseModalities?.Count > 0)
            {
                genConfig.ResponseModalities = _request.Config.ResponseModalities
                    .Select(m => Enum.TryParse<GenerativeAI.Types.Modality>(m.ToString(), true, out var mod) ? mod : GenerativeAI.Types.Modality.MODALITY_UNSPECIFIED)
                    .Where(m => m != GenerativeAI.Types.Modality.MODALITY_UNSPECIFIED)
                    .ToList();
            }

            if (_request.Config.SpeechConfig?.VoiceConfig?.PrebuiltVoiceConfig != null)
            {
                genConfig.SpeechConfig = new GenerativeAI.Types.SpeechConfig
                {
                    VoiceConfig = new GenerativeAI.Types.VoiceConfig
                    {
                        PrebuiltVoiceConfig = new GenerativeAI.Types.PrebuiltVoiceConfig
                        {
                            VoiceName = _request.Config.SpeechConfig!.VoiceConfig!.PrebuiltVoiceConfig!.VoiceName
                        }
                    }
                };
            }

            setup.GenerationConfig = genConfig;

            if (_request.Config.SystemInstruction != null)
            {
                setup.SystemInstruction = new GenerativeAI.Types.Content
                {
                    Role = "system",
                    Parts = new List<GenerativeAI.Types.Part> { new GenerativeAI.Types.Part { Text = _request.Config.SystemInstruction } }
                };
            }

            if (_request.Config.Tools?.Count > 0)
            {
                var tools = new List<GenerativeAI.Types.Tool>();
                foreach (var t in _request.Config.Tools)
                {
                    if (t.FunctionDeclarations?.Count > 0)
                    {
                        var tool = new GenerativeAI.Types.Tool { FunctionDeclarations = new List<GenerativeAI.Types.FunctionDeclaration>() };
                        foreach (var fd in t.FunctionDeclarations)
                        {
                            tool.FunctionDeclarations.Add(new GenerativeAI.Types.FunctionDeclaration
                            {
                                Name = fd.Name,
                                Description = fd.Description,
                                // Note: Schema conversion might be needed if types differ, but assuming they serialize compatibly or are the same type.
                                // If they are different types, we'd need to map them. For now, we serialize/deserialize to map.
                                Parameters = fd.Parameters != null 
                                    ? JsonSerializer.Deserialize<GenerativeAI.Types.Schema>(JsonSerializer.Serialize(fd.Parameters))
                                    : null
                            });
                        }
                        tools.Add(tool);
                    }
                }
                if (tools.Count > 0)
                {
                    setup.Tools = tools.ToArray();
                }
            }
        }

        var msg = new BidiClientMessage { Setup = setup };
        await SendMessageAsync(msg, cancellationToken);

        // Wait for setup complete
        // In a real implementation, we should wait until _setupComplete is true.
        // The receive loop will set it.
        var timeout = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        while (!_setupComplete && !cancellationToken.IsCancellationRequested)
        {
            await Task.WhenAny(Task.Delay(100, cancellationToken), timeout);
            if (timeout.IsCompleted)
            {
                throw new TimeoutException("Timed out waiting for BidiSetupComplete.");
            }
        }
    }

    private async Task SendMessageAsync(BidiClientMessage message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, s_jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    public override async Task SendHistoryAsync(IEnumerable<AdkContent> history, CancellationToken cancellationToken = default)
    {
        // Convert AdkContent to GenerativeAI.Types.Content
        var turns = history.Select(h => new GenerativeAI.Types.Content
        {
            Role = h.Role,
            Parts = h.Parts?.Select(p => new GenerativeAI.Types.Part { Text = p.Text }).ToList() ?? new List<GenerativeAI.Types.Part>()
        }).ToArray();

        var msg = new BidiClientMessage
        {
            ClientContent = new BidiClientContent
            {
                Turns = turns,
                TurnComplete = true
            }
        };

        await SendMessageAsync(msg, cancellationToken);
    }

    public override async Task SendContentAsync(AdkContent content, CancellationToken cancellationToken = default)
    {
        // Check if it's a tool response
        var functionResponses = content.Parts?.Where(p => p.FunctionResponse != null).Select(p => p.FunctionResponse!).ToList();
        if (functionResponses != null && functionResponses.Count > 0)
        {
            var msg = new BidiClientMessage
            {
                ToolResponse = new BidiToolResponse
                {
                    FunctionResponses = functionResponses.Select(fr => new
                    {
                        name = fr.Name,
                        response = fr.Response,
                        id = fr.Id
                    }).ToArray()
                }
            };
            await SendMessageAsync(msg, cancellationToken);
            return;
        }

        var turn = new GenerativeAI.Types.Content
        {
            Role = content.Role,
            Parts = content.Parts?.Select(p => new GenerativeAI.Types.Part { Text = p.Text }).ToList() ?? new List<GenerativeAI.Types.Part>()
        };

        var msgContent = new BidiClientMessage
        {
            ClientContent = new BidiClientContent
            {
                Turns = new[] { turn },
                TurnComplete = true
            }
        };

        await SendMessageAsync(msgContent, cancellationToken);
    }

    public override async Task SendRealtimeAsync(AdkPart part, CancellationToken cancellationToken = default)
    {
        if (part.InlineData != null)
        {
            var msg = new BidiClientMessage
            {
                RealtimeInput = new BidiRealtimeInput
                {
                    MediaChunks = new[]
                    {
                        new BidiMediaChunk
                        {
                            MimeType = part.InlineData.MimeType,
                            Data = part.InlineData.Data
                        }
                    }
                }
            };
            await SendMessageAsync(msg, cancellationToken);
        }
    }

    public override IAsyncEnumerable<LlmResponse> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        return _receiveChannel.Reader.ReadAllAsync(cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 64];
        var messageBuffer = new List<byte>();

        try
        {
            while (_ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
                {
                    messageBuffer.AddRange(buffer.Take(result.Count));

                    if (!result.EndOfMessage)
                    {
                        continue;
                    }

                    var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    messageBuffer.Clear();

                    var serverMessage = JsonSerializer.Deserialize<BidiServerMessage>(json, s_jsonOptions);

                    if (serverMessage?.SetupComplete != null)
                    {
                        _setupComplete = true;
                    }
                    else if (serverMessage?.ServerContent != null)
                    {
                        var content = serverMessage.ServerContent;
                        var parts = new List<AdkPart>();
                        if (content.ModelTurn?.Parts != null)
                        {
                            foreach (var p in content.ModelTurn.Parts)
                            {
                                if (!string.IsNullOrEmpty(p.Text))
                                {
                                    parts.Add(new AdkPart { Text = p.Text });
                                }
                                else if (p.InlineData != null)
                                {
                                    parts.Add(new AdkPart 
                                    { 
                                        InlineData = new GoogleAdk.Core.Abstractions.Models.InlineData 
                                        { 
                                            MimeType = p.InlineData.MimeType ?? string.Empty, 
                                            Data = p.InlineData.Data ?? string.Empty
                                        } 
                                    });
                                }
                            }
                        }

                        if (parts.Count > 0 || content.TurnComplete)
                        {
                            var response = new LlmResponse
                            {
                                Content = new AdkContent
                                {
                                    Role = "model",
                                    Parts = parts
                                },
                                Partial = !content.TurnComplete,
                                TurnComplete = content.TurnComplete
                            };
                            await _receiveChannel.Writer.WriteAsync(response, cancellationToken);
                        }
                    }
                    else if (serverMessage?.ToolCall != null)
                    {
                        var toolCall = serverMessage.ToolCall;
                        var parts = new List<AdkPart>();
                        foreach (var fc in toolCall.FunctionCalls)
                        {
                            Dictionary<string, object?>? argsDict = null;
                            if (fc.Args != null)
                            {
                                var argsJson = JsonSerializer.Serialize(fc.Args);
                                argsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson);
                            }

                            parts.Add(new AdkPart
                            {
                                FunctionCall = new AdkFunctionCall
                                {
                                    Name = fc.Name,
                                    Args = argsDict,
                                    Id = fc.Id
                                }
                            });
                        }

                        if (parts.Count > 0)
                        {
                            var response = new LlmResponse
                            {
                                Content = new AdkContent
                                {
                                    Role = "model",
                                    Parts = parts
                                },
                                Partial = false,
                                TurnComplete = true
                            };
                            await _receiveChannel.Writer.WriteAsync(response, cancellationToken);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _receiveChannel.Writer.TryComplete(ex);
        }
        finally
        {
            _receiveChannel.Writer.TryComplete();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_ws.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None);
            }
            catch { }
        }
        _ws.Dispose();
        _cts.Dispose();
        if (_receiveLoopTask != null)
        {
            try
            {
                await _receiveLoopTask;
            }
            catch { }
        }
    }
}