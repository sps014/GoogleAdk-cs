// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoogleAdk.Core.A2a;

public sealed class MessageSendParams
{
    [JsonPropertyName("message")]
    public Message Message { get; set; } = new();

    [JsonPropertyName("configuration")]
    public MessageSendConfiguration? Configuration { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}

public sealed class MessageSendConfiguration
{
    [JsonPropertyName("acceptedOutputModes")]
    public List<string>? AcceptedOutputModes { get; set; }

    [JsonPropertyName("historyLength")]
    public int? HistoryLength { get; set; }

    [JsonPropertyName("pushNotificationConfig")]
    public PushNotificationConfig? PushNotificationConfig { get; set; }

    [JsonPropertyName("blocking")]
    public bool? Blocking { get; set; }
}

public sealed class PushNotificationConfig
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

public sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("result")]
    public JsonElement Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}

public sealed class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

public sealed class RestSendResponse
{
    [JsonPropertyName("message")]
    public Message? Message { get; set; }

    [JsonPropertyName("task")]
    public Task? Task { get; set; }
}

public sealed class A2aClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _transport;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public A2aClient(string baseUrl, string transport, HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _transport = transport;
        _http = httpClient ?? new HttpClient();
    }

    public async global::System.Threading.Tasks.Task<IA2aEvent> SendMessageAsync(MessageSendParams parameters, CancellationToken cancellationToken = default)
    {
        if (_transport.Equals("JSONRPC", StringComparison.OrdinalIgnoreCase))
        {
            var req = new JsonRpcRequest
            {
                Method = "message/send",
                Params = parameters,
            };
            var response = await PostJsonAsync(_baseUrl, req, cancellationToken);
            var rpc = await ReadJsonAsync<JsonRpcResponse>(response, cancellationToken);
            if (rpc.Error != null) throw new InvalidOperationException(rpc.Error.Message);
            return ParseA2aEvent(rpc.Result);
        }

        var restUrl = $"{_baseUrl}/message:send";
        var restResponse = await PostJsonAsync(restUrl, parameters, cancellationToken);
        var rest = await ReadJsonAsync<RestSendResponse>(restResponse, cancellationToken);
        if (rest.Task != null) return rest.Task;
        if (rest.Message != null) return rest.Message;
        throw new InvalidOperationException("Invalid REST response.");
    }

    public async IAsyncEnumerable<IA2aEvent> SendMessageStreamAsync(
        MessageSendParams parameters,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_transport.Equals("JSONRPC", StringComparison.OrdinalIgnoreCase))
        {
            var req = new JsonRpcRequest
            {
                Method = "message/stream",
                Params = parameters,
            };
            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(req, _jsonOptions), Encoding.UTF8, "application/json"),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await foreach (var evt in ReadSseAsync(response, isJsonRpc: true, cancellationToken))
                yield return evt;
            yield break;
        }

        var restUrl = $"{_baseUrl}/message:stream";
        var restRequest = new HttpRequestMessage(HttpMethod.Post, restUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(parameters, _jsonOptions), Encoding.UTF8, "application/json"),
        };
        restRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var restResponse = await _http.SendAsync(restRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        restResponse.EnsureSuccessStatusCode();
        await foreach (var evt in ReadSseAsync(restResponse, isJsonRpc: false, cancellationToken))
            yield return evt;
    }

    private async global::System.Threading.Tasks.Task<HttpResponseMessage> PostJsonAsync(string url, object body, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var response = await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private async global::System.Threading.Tasks.Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions)
               ?? throw new InvalidOperationException("Failed to parse response.");
    }

    private async IAsyncEnumerable<IA2aEvent> ReadSseAsync(
        HttpResponseMessage response,
        bool isJsonRpc,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            if (!line.StartsWith("data:")) continue;
            var payload = line.Substring(5).Trim();
            if (string.IsNullOrWhiteSpace(payload)) continue;

            if (isJsonRpc)
            {
                var rpc = JsonSerializer.Deserialize<JsonRpcResponse>(payload, _jsonOptions);
                if (rpc?.Error != null) throw new InvalidOperationException(rpc.Error.Message);
                if (rpc == null) continue;
                yield return ParseA2aEvent(rpc.Result);
            }
            else
            {
                var element = JsonSerializer.Deserialize<JsonElement>(payload, _jsonOptions);
                yield return ParseA2aEvent(element);
            }
        }
    }

    private IA2aEvent ParseA2aEvent(JsonElement element)
    {
        if (!element.TryGetProperty("kind", out var kindProp))
            throw new InvalidOperationException("A2A event missing kind.");
        var kind = kindProp.GetString();
        return kind switch
        {
            "message" => element.Deserialize<Message>(_jsonOptions)!,
            "task" => element.Deserialize<Task>(_jsonOptions)!,
            "status-update" => element.Deserialize<TaskStatusUpdateEvent>(_jsonOptions)!,
            "artifact-update" => element.Deserialize<TaskArtifactUpdateEvent>(_jsonOptions)!,
            _ => throw new InvalidOperationException($"Unknown A2A event kind: {kind}"),
        };
    }
}

