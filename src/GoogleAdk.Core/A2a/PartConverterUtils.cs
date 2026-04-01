// Copyright 2026 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GoogleAdk.Core.Abstractions.Models;

namespace GoogleAdk.Core.A2a;

public static class PartConverterUtils
{
    private const string FunctionCallType = "function_call";
    private const string FunctionResponseType = "function_response";
    private const string CodeExecutionResultType = "code_execution_result";
    private const string ExecutableCodeType = "executable_code";

    public static List<A2aPart> ToA2aParts(List<Part>? parts, List<string>? longRunningToolIds = null)
    {
        if (parts == null) return new List<A2aPart>();
        return parts.Select(p => ToA2aPart(p, longRunningToolIds ?? new List<string>())).ToList();
    }

    public static A2aPart ToA2aPart(Part part, List<string> longRunningToolIds)
    {
        if (part.Text != null)
            return ToA2aTextPart(part);

        if (part.InlineData != null || part.FileData != null)
            return ToA2aFilePart(part);

        return ToA2aDataPart(part, longRunningToolIds);
    }

    public static A2aPart ToA2aTextPart(Part part)
    {
        var a2aPart = new A2aPart { Kind = "text", Text = part.Text ?? string.Empty };
        if (part.Thought == true)
        {
            a2aPart.Metadata = new Dictionary<string, object?>
            {
                [A2aMetadataKeys.Thought] = true,
            };
        }
        return a2aPart;
    }

    public static A2aPart ToA2aFilePart(Part part)
    {
        var metadata = new Dictionary<string, object?>();

        if (part.FileData != null)
        {
            return new A2aPart
            {
                Kind = "file",
                File = new A2aFile
                {
                    Uri = part.FileData.FileUri,
                    MimeType = part.FileData.MimeType,
                    Name = part.FileData.Name,
                },
                Metadata = metadata,
            };
        }

        if (part.InlineData != null)
        {
            return new A2aPart
            {
                Kind = "file",
                File = new A2aFile
                {
                    Bytes = part.InlineData.Data,
                    MimeType = part.InlineData.MimeType,
                },
                Metadata = metadata,
            };
        }

        throw new InvalidOperationException("Not a file part.");
    }

    public static A2aPart ToA2aDataPart(Part part, List<string> longRunningToolIds)
    {
        string? dataPartType = null;
        object? data = null;

        if (part.FunctionCall != null)
        {
            dataPartType = FunctionCallType;
            data = part.FunctionCall;
        }
        else if (part.FunctionResponse != null)
        {
            dataPartType = FunctionResponseType;
            data = part.FunctionResponse;
        }
        else if (part.ExecutableCode != null)
        {
            dataPartType = ExecutableCodeType;
            data = part.ExecutableCode;
        }
        else if (part.CodeExecutionResult != null)
        {
            dataPartType = CodeExecutionResultType;
            data = part.CodeExecutionResult;
        }

        var metadata = new Dictionary<string, object?>
        {
            [A2aMetadataKeys.DataPartType] = dataPartType ?? string.Empty,
        };

        if (part.FunctionCall?.Id != null && longRunningToolIds.Contains(part.FunctionCall.Id))
            metadata[A2aMetadataKeys.IsLongRunning] = true;
        if (part.FunctionResponse?.Id != null && longRunningToolIds.Contains(part.FunctionResponse.Id))
            metadata[A2aMetadataKeys.IsLongRunning] = true;

        return new A2aPart
        {
            Kind = "data",
            Data = data != null ? ToDictionary(data) : new Dictionary<string, object?>(),
            Metadata = metadata,
        };
    }

    public static Content ToContent(Message message)
    {
        return new Content
        {
            Role = message.Role == MessageRole.User ? "user" : "model",
            Parts = ToParts(message.Parts),
        };
    }

    public static List<Part> ToParts(List<A2aPart> a2aParts)
    {
        return a2aParts.Select(ToPart).ToList();
    }

    public static Part ToPart(A2aPart a2aPart)
    {
        return a2aPart.Kind switch
        {
            "text" => ToPartText(a2aPart),
            "file" => ToPartFile(a2aPart),
            "data" => ToPartData(a2aPart),
            _ => throw new InvalidOperationException($"Unknown part kind: {a2aPart.Kind}"),
        };
    }

    public static Part ToPartText(A2aPart a2aPart)
    {
        return new Part
        {
            Text = a2aPart.Text,
            Thought = a2aPart.Metadata != null &&
                      a2aPart.Metadata.TryGetValue(A2aMetadataKeys.Thought, out var thought) &&
                      thought is bool b && b,
        };
    }

    public static Part ToPartFile(A2aPart a2aPart)
    {
        var part = new Part();
        if (a2aPart.File == null)
            return part;

        if (!string.IsNullOrWhiteSpace(a2aPart.File.Bytes))
        {
            part.InlineData = new InlineData
            {
                Data = a2aPart.File.Bytes!,
                MimeType = a2aPart.File.MimeType ?? string.Empty,
            };
        }
        else
        {
            part.FileData = new FileData
            {
                FileUri = a2aPart.File.Uri,
                MimeType = a2aPart.File.MimeType,
                Name = a2aPart.File.Name,
            };
        }

        return part;
    }

    public static Part ToPartData(A2aPart a2aPart)
    {
        var part = new Part();
        var type = a2aPart.Metadata != null && a2aPart.Metadata.TryGetValue(A2aMetadataKeys.DataPartType, out var value)
            ? value?.ToString()
            : null;

        if (type == FunctionCallType)
        {
            part.FunctionCall = ToObject<FunctionCall>(a2aPart.Data);
            return part;
        }

        if (type == FunctionResponseType)
        {
            part.FunctionResponse = ToObject<FunctionResponse>(a2aPart.Data);
            return part;
        }

        if (type == ExecutableCodeType)
        {
            part.ExecutableCode = ToObject<ExecutableCode>(a2aPart.Data);
            return part;
        }

        if (type == CodeExecutionResultType)
        {
            part.CodeExecutionResult = ToObject<CodeExecutionResult>(a2aPart.Data);
            return part;
        }

        part.Text = JsonSerializer.Serialize(a2aPart.Data);
        return part;
    }

    private static Dictionary<string, object?> ToDictionary(object input)
    {
        var json = JsonSerializer.Serialize(input);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
    }

    private static T? ToObject<T>(Dictionary<string, object?>? dict)
    {
        if (dict == null) return default;
        var json = JsonSerializer.Serialize(dict);
        return JsonSerializer.Deserialize<T>(json);
    }
}

