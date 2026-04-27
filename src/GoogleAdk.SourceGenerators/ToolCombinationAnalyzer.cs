using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GoogleAdk.SourceGenerators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ToolCombinationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ADK004";
    private const string Title = "Invalid Tool Combination";
    private const string MessageFormat = "Native grounding tools (like {0}) cannot be paired with other function tools in the same agent. Use a SequentialAgent or ParallelAgent to orchestrate them instead.";
    private const string Category = "Usage";

#pragma warning disable RS2008
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Native grounding tools cannot be combined with standard function tools in a single agent.");
#pragma warning restore RS2008

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    private static readonly string[] NativeGroundingTools = new[]
    {
        "GoogleSearchTool",
        "VertexAiSearchTool",
        "UrlContextTool",
        "EnterpriseWebSearchTool",
        "GoogleMapsGroundingTool",
        "VertexAiRagRetrievalTool"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.SimpleAssignmentExpression);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AssignmentExpressionSyntax assignment)
            return;

        string? propertyName = null;

        if (assignment.Left is IdentifierNameSyntax id)
        {
            propertyName = id.Identifier.Text;
        }
        else if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
        {
            propertyName = memberAccess.Name.Identifier.Text;
        }

        if (propertyName != "Tools")
            return;

        AnalyzeToolsCollection(context, assignment.Right);
    }

    private void AnalyzeToolsCollection(SyntaxNodeAnalysisContext context, ExpressionSyntax toolsExpression)
    {
        var elements = new System.Collections.Generic.List<ExpressionSyntax>();

        if (toolsExpression is CollectionExpressionSyntax collectionExpression)
        {
            foreach (var element in collectionExpression.Elements)
            {
                if (element is ExpressionElementSyntax exprElement)
                {
                    elements.Add(exprElement.Expression);
                }
            }
        }
        else if (toolsExpression is ObjectCreationExpressionSyntax objCreation && objCreation.Initializer != null)
        {
            foreach (var expr in objCreation.Initializer.Expressions)
            {
                elements.Add(expr);
            }
        }
        else if (toolsExpression is ImplicitObjectCreationExpressionSyntax implicitObjCreation && implicitObjCreation.Initializer != null)
        {
            foreach (var expr in implicitObjCreation.Initializer.Expressions)
            {
                elements.Add(expr);
            }
        }
        else if (toolsExpression is ArrayCreationExpressionSyntax arrayCreation && arrayCreation.Initializer != null)
        {
            foreach (var expr in arrayCreation.Initializer.Expressions)
            {
                elements.Add(expr);
            }
        }
        else if (toolsExpression is ImplicitArrayCreationExpressionSyntax implicitArrayCreation && implicitArrayCreation.Initializer != null)
        {
            foreach (var expr in implicitArrayCreation.Initializer.Expressions)
            {
                elements.Add(expr);
            }
        }

        if (elements.Count < 2)
            return;

        bool hasNativeGroundingTool = false;
        bool hasStandardTool = false;
        string? nativeToolName = null;

        foreach (var element in elements)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(element);
            var type = typeInfo.Type;

            string? typeName = null;

            if (type != null && type.TypeKind != TypeKind.Error)
            {
                typeName = type.Name;
            }
            else
            {
                // Fallback to syntax
                if (element is ObjectCreationExpressionSyntax objCreation)
                {
                    typeName = objCreation.Type.ToString();
                }
                else if (element is ImplicitObjectCreationExpressionSyntax)
                {
                    // Can't infer name from syntax
                }
                else if (element is IdentifierNameSyntax idName)
                {
                    typeName = idName.Identifier.Text;
                }
                else if (element is MemberAccessExpressionSyntax memberAccess)
                {
                    typeName = memberAccess.Name.Identifier.Text;
                }
            }

            if (typeName != null)
            {
                // Remove namespace prefixes if any
                var lastDot = typeName.LastIndexOf('.');
                if (lastDot >= 0)
                {
                    typeName = typeName.Substring(lastDot + 1);
                }

                if (IsNativeGroundingTool(typeName))
                {
                    if (!IsBypassedVertexAiSearch(element, context.SemanticModel))
                    {
                        hasNativeGroundingTool = true;
                        nativeToolName = typeName;
                    }
                }
                else
                {
                    hasStandardTool = true;
                }
            }
            else
            {
                // If we can't determine the type, assume it's a standard tool to be safe
                hasStandardTool = true;
            }
        }

        if (hasNativeGroundingTool && hasStandardTool)
        {
            var diagnostic = Diagnostic.Create(Rule, toolsExpression.GetLocation(), nativeToolName ?? "GoogleSearchTool");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private bool IsNativeGroundingTool(string typeName)
    {
        return NativeGroundingTools.Contains(typeName);
    }

    private bool IsBypassedVertexAiSearch(ExpressionSyntax element, SemanticModel semanticModel)
    {
        if (element is ObjectCreationExpressionSyntax objCreation)
        {
            var typeName = objCreation.Type.ToString();
            if (typeName.EndsWith("VertexAiSearchTool"))
            {
                return HasBypassArgument(objCreation.ArgumentList, semanticModel, objCreation);
            }
        }
        else if (element is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            var typeInfo = semanticModel.GetTypeInfo(implicitCreation);
            if (typeInfo.Type?.Name == "VertexAiSearchTool")
            {
                return HasBypassArgument(implicitCreation.ArgumentList, semanticModel, implicitCreation);
            }
        }

        return false;
    }

    private bool HasBypassArgument(ArgumentListSyntax? argumentList, SemanticModel semanticModel, ExpressionSyntax creationExpression)
    {
        if (argumentList == null) return false;

        foreach (var arg in argumentList.Arguments)
        {
            if (arg.NameColon != null && arg.NameColon.Name.Identifier.Text == "bypassMultiToolsLimit")
            {
                if (arg.Expression.IsKind(SyntaxKind.TrueLiteralExpression))
                {
                    return true;
                }
            }
        }

        // Check positional arguments via semantic model
        var symbolInfo = semanticModel.GetSymbolInfo(creationExpression);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        {
            for (int i = 0; i < argumentList.Arguments.Count; i++)
            {
                var arg = argumentList.Arguments[i];
                if (arg.NameColon == null && i < methodSymbol.Parameters.Length)
                {
                    if (methodSymbol.Parameters[i].Name == "bypassMultiToolsLimit")
                    {
                        if (arg.Expression.IsKind(SyntaxKind.TrueLiteralExpression))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
