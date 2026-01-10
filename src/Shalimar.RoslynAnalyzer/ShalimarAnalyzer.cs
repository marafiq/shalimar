using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Shalimar.RoslynAnalyzer;

/// <summary>
/// Roslyn analyzer for compile-time diagnostics in Shalimar applications.
/// Provides warnings for common mistakes in route configuration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ShalimarAnalyzer : DiagnosticAnalyzer
{
    public const string MissingPropsTypeId = "SHALIMAR001";
    public const string InvalidRoutePathId = "SHALIMAR002";
    public const string DuplicateRouteId = "SHALIMAR003";
    public const string MissingJsxFileId = "SHALIMAR004";

    private static readonly DiagnosticDescriptor MissingPropsTypeRule = new(
        MissingPropsTypeId,
        title: "AsComponent requires a type argument",
        messageFormat: "AsComponent<T>() requires a type argument specifying the props type",
        category: "Shalimar",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The AsComponent method must specify a props type as a generic argument.");

    private static readonly DiagnosticDescriptor InvalidRoutePathRule = new(
        InvalidRoutePathId,
        title: "Invalid route path",
        messageFormat: "Route path '{0}' is invalid: {1}",
        category: "Shalimar",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Route paths should start with '/' and follow valid URL patterns.");

    private static readonly DiagnosticDescriptor DuplicateRouteRule = new(
        DuplicateRouteId,
        title: "Duplicate route path",
        messageFormat: "Route path '{0}' is already defined. Duplicate routes will cause conflicts.",
        category: "Shalimar",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Each route path should be unique within the application.");

    private static readonly DiagnosticDescriptor MissingJsxFileRule = new(
        MissingJsxFileId,
        title: "Consider adding JSX file reference",
        messageFormat: "Consider adding .WithJsxFile() to specify the React component file for better tooling support",
        category: "Shalimar",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Adding a JSX file reference improves IDE tooling and navigation.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MissingPropsTypeRule,
            InvalidRoutePathRule,
            DuplicateRouteRule,
            MissingJsxFileRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register for invocation expressions to find AsComponent calls
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is an AsComponent call
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;

            if (methodName == "AsComponent")
            {
                AnalyzeAsComponentCall(context, invocation, memberAccess);
            }
        }
    }

    private static void AnalyzeAsComponentCall(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess)
    {
        // Check if generic argument is provided
        if (memberAccess.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count == 0)
        {
            var diagnostic = Diagnostic.Create(
                MissingPropsTypeRule,
                memberAccess.Name.GetLocation());
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Find the parent MapGet/MapPost call to check route path
        var routePath = FindRoutePath(invocation);
        if (routePath != null)
        {
            ValidateRoutePath(context, invocation, routePath);
        }

        // Check if WithJsxFile is chained
        if (!HasWithJsxFileChained(invocation))
        {
            var diagnostic = Diagnostic.Create(
                MissingJsxFileRule,
                invocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string? FindRoutePath(InvocationExpressionSyntax invocation)
    {
        var current = invocation.Parent;
        while (current != null)
        {
            if (current is InvocationExpressionSyntax parentInvocation &&
                parentInvocation.Expression is MemberAccessExpressionSyntax parentMember)
            {
                var methodName = parentMember.Name.Identifier.Text;
                if (methodName.StartsWith("Map"))
                {
                    var args = parentInvocation.ArgumentList.Arguments;
                    if (args.Count > 0 && args[0].Expression is LiteralExpressionSyntax literal)
                    {
                        return literal.Token.ValueText;
                    }
                }
            }
            current = current.Parent;
        }
        return null;
    }

    private static void ValidateRoutePath(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string routePath)
    {
        // Check if route starts with /
        if (!routePath.StartsWith("/"))
        {
            var diagnostic = Diagnostic.Create(
                InvalidRoutePathRule,
                invocation.GetLocation(),
                routePath,
                "Route path must start with '/'");
            context.ReportDiagnostic(diagnostic);
        }

        // Check for invalid characters
        var invalidChars = new[] { ' ', '#', '?', '&' };
        foreach (var c in invalidChars)
        {
            if (routePath.Contains(c.ToString()))
            {
                var diagnostic = Diagnostic.Create(
                    InvalidRoutePathRule,
                    invocation.GetLocation(),
                    routePath,
                    $"Route path contains invalid character '{c}'");
                context.ReportDiagnostic(diagnostic);
                break;
            }
        }
    }

    private static bool HasWithJsxFileChained(InvocationExpressionSyntax invocation)
    {
        // Check if the parent is a member access with WithJsxFile
        if (invocation.Parent is MemberAccessExpressionSyntax parentMember &&
            parentMember.Name.Identifier.Text == "WithJsxFile")
        {
            return true;
        }

        // Check if there's a chained call
        var current = invocation.Parent;
        while (current != null)
        {
            if (current is InvocationExpressionSyntax chainedInvocation &&
                chainedInvocation.Expression is MemberAccessExpressionSyntax chainedMember &&
                chainedMember.Name.Identifier.Text == "WithJsxFile")
            {
                return true;
            }
            current = current.Parent;
        }

        return false;
    }
}
