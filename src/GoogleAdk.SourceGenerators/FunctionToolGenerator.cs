using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GoogleAdk.SourceGenerators;

/// <summary>
/// Incremental source generator that discovers methods decorated with
/// <c>[FunctionTool]</c> and emits a partial class containing ready-to-use
/// <c>FunctionTool</c> instances backed by proper JSON schemas.
/// </summary>
[Generator]
public sealed class FunctionToolGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "GoogleAdk.Core.Abstractions.Tools.FunctionToolAttribute";

    private static readonly DiagnosticDescriptor MissingDocError = new DiagnosticDescriptor(
        id: "ADK001",
        title: "Missing XML Documentation",
        messageFormat: "Method '{0}' is marked as [FunctionTool] but lacks an XML documentation <summary>. LLM tools require descriptions.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidReturnTypeError = new DiagnosticDescriptor(
        id: "ADK002",
        title: "Invalid FunctionTool Return Type",
        messageFormat: "Method '{0}' is marked as [FunctionTool] but returns '{1}'. Tools must return a value (no void/Task-only).",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RequireConfirmationNeedsContextError = new DiagnosticDescriptor(
        id: "ADK003",
        title: "RequireConfirmation Requires AgentContext",
        messageFormat: "Method '{0}' sets RequireConfirmation=true but does not accept an AgentContext parameter.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var extracted = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, ct) => ExtractModel(ctx, ct));

        // Report diagnostics
        context.RegisterSourceOutput(extracted, static (spc, result) =>
        {
            if (!result.Diagnostics.IsDefaultOrEmpty)
            {
                foreach (var diag in result.Diagnostics)
                {
                    spc.ReportDiagnostic(diag);
                }
            }
        });

        // 2. Emit source for each containing class
        var validModels = extracted
            .Where(static r => r.Model is not null)
            .Select(static (r, _) => r.Model!)
            .Collect();

        context.RegisterSourceOutput(validModels, static (spc, models) => Emit(spc, models));
    }

    // ------------------------------------------------------------------
    // Model extraction (runs in the pipeline, must be deterministic)
    // ------------------------------------------------------------------

    private static ExtractionResult ExtractModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method)
            return new ExtractionResult(null, ImmutableArray<Diagnostic>.Empty);

        if (method.ContainingType is null)
            return new ExtractionResult(null, ImmutableArray<Diagnostic>.Empty);

        // Read attribute properties
        var attr = ctx.Attributes.First(a =>
            a.AttributeClass?.ToDisplayString() == AttributeFullName);

        string? nameOverride = null;
        bool isLongRunning = false;
        bool requireConfirmation = false;
        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == "Name" && named.Value.Value is string n)
                nameOverride = n;
            if (named.Key == "IsLongRunning" && named.Value.Value is bool b)
                isLongRunning = b;
            if (named.Key == "RequireConfirmation" && named.Value.Value is bool rc)
                requireConfirmation = rc;
        }

        // Tool name: attribute override or PascalCase → snake_case
        string toolName = nameOverride ?? ToSnakeCase(method.Name);

        // Return type validation (must return a value, not void or Task)
        if (IsInvalidReturnType(method))
        {
            var location = (ctx.TargetNode as MethodDeclarationSyntax)?.Identifier.GetLocation() ?? ctx.TargetNode.GetLocation();
            var returnType = method.ReturnType.ToDisplayString();
            var diag = Diagnostic.Create(InvalidReturnTypeError, location, method.Name, returnType);
            return new ExtractionResult(null, ImmutableArray.Create(diag));
        }

        // XML doc extraction (reads directly from syntax tree to avoid requiring <GenerateDocumentationFile>true</GenerateDocumentationFile>)
        string description = "";
        var paramDescriptions = new Dictionary<string, string>();

        if (ctx.TargetNode is MethodDeclarationSyntax methodSyntax)
        {
            var leadingTriviaStr = methodSyntax.GetLeadingTrivia().ToFullString();
            var rawLines = leadingTriviaStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sbDoc = new StringBuilder();
            sbDoc.AppendLine("<root>");
            int docLinesCount = 0;
            foreach (var line in rawLines)
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("///"))
                {
                    sbDoc.AppendLine(trimmed.Substring(3).TrimStart());
                    docLinesCount++;
                }
            }
            sbDoc.AppendLine("</root>");

            if (docLinesCount > 0)
            {
                try
                {
                    var doc = XDocument.Parse(sbDoc.ToString());
                    description = doc.Descendants("summary").FirstOrDefault()?.Value.Trim() ?? "";
                    foreach (var p in doc.Descendants("param"))
                    {
                        var pName = p.Attribute("name")?.Value;
                        if (pName != null)
                            paramDescriptions[pName] = p.Value.Trim();
                    }
                }
                catch { /* malformed XML – ignore */ }
            }
        }

        // Parameters (skip CancellationToken, AgentContext)
        var parameters = new List<ParamModel>();
        foreach (var p in method.Parameters)
        {
            var typeName = p.Type.ToDisplayString();
            if (typeName == "System.Threading.CancellationToken" ||
                typeName == "GoogleAdk.Core.Agents.AgentContext")
                continue;

            bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated
                              || p.HasExplicitDefaultValue;

            parameters.Add(new ParamModel
            {
                Name = p.Name,
                CSharpType = typeName,
                JsonType = MapJsonType(p.Type),
                Description = paramDescriptions.TryGetValue(p.Name, out var d) ? d : null,
                IsRequired = !isNullable,
            });
        }

        // Determine if method is async
        bool isAsync = method.ReturnType.ToDisplayString().StartsWith("System.Threading.Tasks.Task");

        // Check if method has AgentContext parameter
        bool hasContext = method.Parameters.Any(p =>
            p.Type.ToDisplayString() == "GoogleAdk.Core.Agents.AgentContext");

        // Containing type info
        string containingNamespace = method.ContainingType.ContainingNamespace?.IsGlobalNamespace == true 
            ? "" 
            : method.ContainingType.ContainingNamespace?.ToDisplayString() ?? "";
        string containingTypeName = method.ContainingType.Name;
        bool isContainingTypeStatic = method.ContainingType.IsStatic;

        var model = new ToolModel
        {
            ToolName = toolName,
            Description = description,
            IsLongRunning = isLongRunning,
            RequireConfirmation = requireConfirmation,
            MethodName = method.Name,
            IsAsync = isAsync,
            HasContextParam = hasContext,
            Parameters = parameters,
            ContainingNamespace = containingNamespace,
            ContainingTypeName = containingTypeName,
            IsContainingTypeStatic = isContainingTypeStatic,
            IsStatic = method.IsStatic,
        };

        if (requireConfirmation && !hasContext)
        {
            var location = (ctx.TargetNode as MethodDeclarationSyntax)?.Identifier.GetLocation() ?? ctx.TargetNode.GetLocation();
            var diag = Diagnostic.Create(RequireConfirmationNeedsContextError, location, method.Name);
            return new ExtractionResult(null, ImmutableArray.Create(diag));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            var location = (ctx.TargetNode as MethodDeclarationSyntax)?.Identifier.GetLocation() ?? ctx.TargetNode.GetLocation();
            var diag = Diagnostic.Create(MissingDocError, location, method.Name);
            return new ExtractionResult(model, ImmutableArray.Create(diag));
        }

        return new ExtractionResult(model, ImmutableArray<Diagnostic>.Empty);
    }

    private static bool IsInvalidReturnType(IMethodSymbol method)
    {
        if (method.ReturnsVoid)
            return true;

        if (method.ReturnType is INamedTypeSymbol named &&
            named.ToDisplayString() == "System.Threading.Tasks.Task" &&
            named.TypeArguments.Length == 0)
        {
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------------
    // Code emission
    // ------------------------------------------------------------------

    private static void Emit(SourceProductionContext spc, ImmutableArray<ToolModel> models)
    {
        if (models.IsDefaultOrEmpty)
            return;

        // Group by containing type
        var groups = models.GroupBy(m => $"{m.ContainingNamespace}.{m.ContainingTypeName}");

        foreach (var group in groups)
        {
            var first = group.First();
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using GoogleAdk.Core.Tools;");
            sb.AppendLine("using GoogleAdk.Core.Agents;");
            sb.AppendLine("using GoogleAdk.Core.Abstractions.Tools;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(first.ContainingNamespace))
            {
                sb.AppendLine($"namespace {first.ContainingNamespace};");
                sb.AppendLine();
            }

            var staticModifier = first.IsContainingTypeStatic ? "static " : "";
            sb.AppendLine($"{staticModifier}partial class {first.ContainingTypeName}");
            sb.AppendLine("{");

            // Emit individual static properties for each tool
            foreach (var tool in group)
            {
                var propName = ToPascalProperty(tool.MethodName);
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Auto-generated <see cref=\"FunctionTool\"/> for <see cref=\"{tool.MethodName}\"/>.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    public static FunctionTool {propName}Tool {{ get; }} =");
                EmitToolInstance(sb, tool, indent: 8);
            }

            sb.AppendLine();

            // Emit a static method that returns all tools
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Returns all auto-generated <see cref=\"FunctionTool\"/> instances");
            sb.AppendLine("    /// for methods decorated with <c>[FunctionTool]</c>.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static IReadOnlyList<FunctionTool> GetGeneratedTools()");
            sb.AppendLine("    {");
            sb.AppendLine("        return new FunctionTool[]");
            sb.AppendLine("        {");

            foreach (var tool in group)
            {
                var propName = ToPascalProperty(tool.MethodName);
                sb.AppendLine($"            {propName}Tool,");
            }

            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            var hintName = $"{first.ContainingTypeName}.FunctionTools.g.cs";
            spc.AddSource(hintName, SourceText.From(sb.ToString(), Encoding.UTF8));
        }
    }

    private static void EmitToolInstance(StringBuilder sb, ToolModel tool, int indent = 12)
    {
        var pad = new string(' ', indent);
        var pad2 = new string(' ', indent + 4);
        var pad3 = new string(' ', indent + 8);
        sb.AppendLine($"{pad}new FunctionTool(");
        sb.AppendLine($"{pad2}name: \"{EscapeString(tool.ToolName)}\",");
        sb.AppendLine($"{pad2}description: \"{EscapeString(tool.Description)}\",");

        // Emit the delegate
        if (tool.IsAsync && tool.HasContextParam)
        {
            sb.AppendLine($"{pad2}execute: async (args, ctx) =>");
            sb.AppendLine($"{pad2}{{");
            EmitRequireConfirmation(sb, tool, pad3, returnTask: false);
            EmitMethodCall(sb, tool, "args", "ctx", pad3);
            sb.AppendLine($"{pad2}}},");
        }
        else if (tool.IsAsync)
        {
            sb.AppendLine($"{pad2}execute: async (args, _) =>");
            sb.AppendLine($"{pad2}{{");
            EmitMethodCall(sb, tool, "args", null, pad3);
            sb.AppendLine($"{pad2}}},");
        }
        else if (tool.HasContextParam)
        {
            sb.AppendLine($"{pad2}execute: (args, ctx) =>");
            sb.AppendLine($"{pad2}{{");
            EmitRequireConfirmation(sb, tool, pad3, returnTask: true);
            EmitMethodCallSync(sb, tool, "args", "ctx", pad3);
            sb.AppendLine($"{pad2}}},");
        }
        else
        {
            sb.AppendLine($"{pad2}execute: (args, _) =>");
            sb.AppendLine($"{pad2}{{");
            EmitMethodCallSync(sb, tool, "args", null, pad3);
            sb.AppendLine($"{pad2}}},");
        }

        // Emit parameters schema
        EmitParametersSchema(sb, tool, pad2);

        sb.AppendLine($"{pad2}isLongRunning: {(tool.IsLongRunning ? "true" : "false")});");
    }

    private static void EmitRequireConfirmation(StringBuilder sb, ToolModel tool, string pad, bool returnTask)
    {
        if (!tool.RequireConfirmation)
            return;

        sb.AppendLine($"{pad}if (string.IsNullOrEmpty(ctx.FunctionCallId))");
        sb.AppendLine($"{pad}{{");
        EmitReturn(sb, pad + "    ", returnTask, "new Dictionary<string, object?> { [\"error\"] = \"FunctionCallId is not set.\" }");
        sb.AppendLine($"{pad}}}");
        sb.AppendLine($"{pad}if (!ctx.EventActions.RequestedToolConfirmations.TryGetValue(ctx.FunctionCallId!, out var confirmation))");
        sb.AppendLine($"{pad}{{");
        sb.AppendLine($"{pad}    ctx.EventActions.RequestedToolConfirmations[ctx.FunctionCallId!] = new ToolConfirmation");
        sb.AppendLine($"{pad}    {{");
        sb.AppendLine($"{pad}        FunctionCallId = ctx.FunctionCallId!");
        sb.AppendLine($"{pad}    }};");
        EmitReturn(sb, pad + "    ", returnTask, "new Dictionary<string, object?> { [\"partial\"] = \"This tool call needs external confirmation before completion.\" }");
        sb.AppendLine($"{pad}}}");
        sb.AppendLine($"{pad}if (confirmation.Accepted != true)");
        sb.AppendLine($"{pad}{{");
        EmitReturn(sb, pad + "    ", returnTask, "new Dictionary<string, object?> { [\"error\"] = \"Tool call rejected from confirmation flow.\" }");
        sb.AppendLine($"{pad}}}");
    }

    private static void EmitReturn(StringBuilder sb, string pad, bool returnTask, string expression)
    {
        if (returnTask)
            sb.AppendLine($"{pad}return Task.FromResult<object?>({expression});");
        else
            sb.AppendLine($"{pad}return {expression};");
    }

    private static void EmitMethodCall(StringBuilder sb, ToolModel tool, string argsVar, string? ctxVar, string pad)
    {
        var callArgs = BuildCallArgs(tool, argsVar, ctxVar);
        var prefix = tool.IsStatic ? $"{tool.ContainingTypeName}" : throw new InvalidOperationException("Non-static methods not supported");
        sb.AppendLine($"{pad}var result = await {prefix}.{tool.MethodName}({callArgs});");
        sb.AppendLine($"{pad}return (object?)result;");
    }

    private static void EmitMethodCallSync(StringBuilder sb, ToolModel tool, string argsVar, string? ctxVar, string pad)
    {
        var callArgs = BuildCallArgs(tool, argsVar, ctxVar);
        var prefix = tool.IsStatic ? $"{tool.ContainingTypeName}" : throw new InvalidOperationException("Non-static methods not supported");

        if (tool.IsAsync)
        {
            sb.AppendLine($"{pad}return {prefix}.{tool.MethodName}({callArgs});");
        }
        else
        {
            sb.AppendLine($"{pad}return Task.FromResult<object?>({prefix}.{tool.MethodName}({callArgs}));");
        }
    }

    private static string BuildCallArgs(ToolModel tool, string argsVar, string? ctxVar)
    {
        var parts = new List<string>();
        foreach (var p in tool.Parameters)
        {
            var extract = EmitArgExtract(p, argsVar);
            parts.Add(extract);
        }
        if (ctxVar != null)
            parts.Add(ctxVar);
        return string.Join(", ", parts);
    }

    private static string EmitArgExtract(ParamModel p, string argsVar)
    {
        var convert = GetConvertExpression(p.CSharpType, $"{argsVar}[\"{p.Name}\"]");
        if (p.IsRequired)
            return convert;

        var convertOpt = GetConvertExpression(p.CSharpType, $"_{p.Name}!");
        return $"{argsVar}.TryGetValue(\"{p.Name}\", out var _{p.Name}) ? {convertOpt} : default";
    }

    private static string GetConvertExpression(string csharpType, string expr)
    {
        switch (csharpType)
        {
            case "int":
            case "System.Int32":
                return $"GoogleAdk.Core.Tools.FunctionToolArgs.Get<int>({expr})";
            case "long":
            case "System.Int64":
                return $"GoogleAdk.Core.Tools.FunctionToolArgs.Get<long>({expr})";
            case "float":
            case "System.Single":
                return $"GoogleAdk.Core.Tools.FunctionToolArgs.Get<float>({expr})";
            case "double":
            case "System.Double":
                return $"GoogleAdk.Core.Tools.FunctionToolArgs.Get<double>({expr})";
            case "bool":
            case "System.Boolean":
                return $"GoogleAdk.Core.Tools.FunctionToolArgs.Get<bool>({expr})";
            case "string":
            case "System.String":
                return $"GoogleAdk.Core.Tools.FunctionToolArgs.Get<string>({expr})";
            default:
                return $"GoogleAdk.Core.Tools.FunctionToolArgs.Get<{csharpType}>({expr})";
        }
    }

    private static void EmitParametersSchema(StringBuilder sb, ToolModel tool, string pad)
    {
        var pad2 = pad + "    ";
        var pad3 = pad2 + "    ";
        var pad4 = pad3 + "    ";
        if (tool.Parameters.Count == 0)
        {
            sb.AppendLine($"{pad}parameters: null,");
            return;
        }

        sb.AppendLine($"{pad}parameters: new Dictionary<string, object?>");
        sb.AppendLine($"{pad}{{");
        sb.AppendLine($"{pad2}[\"type\"] = \"object\",");
        sb.AppendLine($"{pad2}[\"properties\"] = new Dictionary<string, object?>");
        sb.AppendLine($"{pad2}{{");

        foreach (var p in tool.Parameters)
        {
            sb.AppendLine($"{pad3}[\"{p.Name}\"] = new Dictionary<string, object?>");
            sb.AppendLine($"{pad3}{{");
            sb.AppendLine($"{pad4}[\"type\"] = \"{p.JsonType}\",");
            if (p.Description != null)
                sb.AppendLine($"{pad4}[\"description\"] = \"{EscapeString(p.Description)}\",");
            sb.AppendLine($"{pad3}}},");
        }

        sb.AppendLine($"{pad2}}},");

        // Required array
        var required = tool.Parameters.Where(p => p.IsRequired).ToList();
        if (required.Count > 0)
        {
            var names = string.Join(", ", required.Select(r => $"\"{r.Name}\""));
            sb.AppendLine($"{pad2}[\"required\"] = new List<string> {{ {names} }},");
        }

        sb.AppendLine($"{pad}}},");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Keeps the method name as-is (already PascalCase) for property naming.
    /// </summary>
    private static string ToPascalProperty(string methodName) => methodName;

    private static string MapJsonType(ITypeSymbol type)
    {
        var display = type.ToDisplayString();
        // Strip nullable
        if (type is INamedTypeSymbol named && named.ConstructedFrom.ToDisplayString() == "System.Nullable<T>")
            display = named.TypeArguments[0].ToDisplayString();

        switch (display)
        {
            case "string":
            case "System.String":
                return "string";
            case "int":
            case "System.Int32":
            case "long":
            case "System.Int64":
                return "integer";
            case "float":
            case "System.Single":
            case "double":
            case "System.Double":
            case "decimal":
            case "System.Decimal":
                return "number";
            case "bool":
            case "System.Boolean":
                return "boolean";
            default:
                if (display.Contains("[]") || display.StartsWith("System.Collections.Generic.List"))
                    return "array";
                return "object";
        }
    }

    private static string EscapeString(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
}

// ------------------------------------------------------------------
// Internal models for the pipeline
// ------------------------------------------------------------------

internal sealed class ToolModel : IEquatable<ToolModel>
{
    public string ToolName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsLongRunning { get; set; }
    public bool RequireConfirmation { get; set; }
    public string MethodName { get; set; } = "";
    public bool IsAsync { get; set; }
    public bool HasContextParam { get; set; }
    public bool IsStatic { get; set; }
    public List<ParamModel> Parameters { get; set; } = new();
    public string ContainingNamespace { get; set; } = "";
    public string ContainingTypeName { get; set; } = "";
    public bool IsContainingTypeStatic { get; set; }

    public bool Equals(ToolModel? other)
    {
        if (other is null) return false;
        return ToolName == other.ToolName
            && ContainingNamespace == other.ContainingNamespace
            && ContainingTypeName == other.ContainingTypeName
            && MethodName == other.MethodName;
    }

    public override bool Equals(object? obj) => Equals(obj as ToolModel);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (ToolName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ContainingNamespace?.GetHashCode() ?? 0);
            hash = hash * 31 + (ContainingTypeName?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

internal sealed class ParamModel
{
    public string Name { get; set; } = "";
    public string CSharpType { get; set; } = "";
    public string JsonType { get; set; } = "";
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
}

internal readonly struct ExtractionResult
{
    public ToolModel? Model { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public ExtractionResult(ToolModel? model, ImmutableArray<Diagnostic> diagnostics)
    {
        Model = model;
        Diagnostics = diagnostics;
    }
}
