// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A tool that wraps a C# function/delegate. The simplest way to create a tool.
/// </summary>
public class FunctionTool : BaseTool
{
    private readonly Func<Dictionary<string, object?>, AgentContext, Task<object?>> _execute;
    private readonly FunctionDeclaration? _declaration;

    /// <summary>
    /// Creates a FunctionTool with explicit name, description, and execute delegate.
    /// </summary>
    /// <param name="name">Tool name (must match what the model will call).</param>
    /// <param name="description">Tool description for the model.</param>
    /// <param name="execute">The function to execute. Receives args dict and context.</param>
    /// <param name="parameters">Optional JSON Schema for parameters.</param>
    /// <param name="isLongRunning">Whether this is a long-running tool.</param>
    public FunctionTool(
        string name,
        string description,
        Func<Dictionary<string, object?>, AgentContext, Task<object?>> execute,
        Dictionary<string, object?>? parameters = null,
        bool isLongRunning = false)
        : base(name, description, isLongRunning)
    {
        _execute = execute;
        _declaration = new FunctionDeclaration
        {
            Name = name,
            Description = description,
            Parameters = parameters
        };
    }

    /// <summary>
    /// Convenience overload for simple sync functions.
    /// </summary>
    public FunctionTool(
        string name,
        string description,
        Func<Dictionary<string, object?>, object?> execute,
        Dictionary<string, object?>? parameters = null,
        bool isLongRunning = false)
        : this(name, description, (args, _) => Task.FromResult(execute(args)), parameters, isLongRunning) { }

    public override FunctionDeclaration? GetDeclaration() => _declaration;

    public override Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
        => _execute(args, context);
}

/// <summary>
/// A tool that wraps a strongly-typed C# function with automatic JSON schema generation.
/// </summary>
/// <typeparam name="TArgs">The argument type. Should be a simple class/record with properties.</typeparam>
public class FunctionTool<TArgs> : BaseTool where TArgs : class, new()
{
    private readonly Func<TArgs, AgentContext, Task<object?>> _execute;
    private readonly FunctionDeclaration _declaration;

    public FunctionTool(
        string name,
        string description,
        Func<TArgs, AgentContext, Task<object?>> execute,
        bool isLongRunning = false)
        : base(name, description, isLongRunning)
    {
        _execute = execute;
        _declaration = new FunctionDeclaration
        {
            Name = name,
            Description = description,
            Parameters = GenerateSchemaFromType(typeof(TArgs))
        };
    }

    public override FunctionDeclaration? GetDeclaration() => _declaration;

    public override async Task<object?> RunAsync(Dictionary<string, object?> args, AgentContext context)
    {
        var typed = System.Text.Json.JsonSerializer.Deserialize<TArgs>(
            System.Text.Json.JsonSerializer.Serialize(args))
            ?? new TArgs();
        return await _execute(typed, context);
    }

    private static Dictionary<string, object?> GenerateSchemaFromType(Type type)
    {
        var properties = new Dictionary<string, object?>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var propSchema = new Dictionary<string, object?>
            {
                ["type"] = GetJsonType(prop.PropertyType)
            };
            properties[prop.Name] = propSchema;

            // Non-nullable value types and strings without ? are required
            if (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null)
                required.Add(prop.Name);
        }

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
        };

        if (required.Count > 0)
            schema["required"] = required;

        return schema;
    }

    private static string GetJsonType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string)) return "string";
        if (underlying == typeof(int) || underlying == typeof(long)) return "integer";
        if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal)) return "number";
        if (underlying == typeof(bool)) return "boolean";
        if (underlying.IsArray || (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(List<>))) return "array";
        return "object";
    }
}
