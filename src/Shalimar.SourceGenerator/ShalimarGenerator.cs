using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Shalimar.SourceGenerator;

/// <summary>
/// Roslyn source generator that produces C# routes and TypeScript types.
/// TypeScript is embedded in C# files within /* SHALIMAR_TS: filename */ ... /* END_SHALIMAR_TS */ blocks.
/// </summary>
[Generator]
public class ShalimarGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all invocations of AsComponent<T>()
        var componentCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsAsComponentCall(node),
                transform: static (ctx, _) => GetComponentInfo(ctx))
            .Where(static info => info is not null)
            .Collect();

        // Combine with compilation
        var compilationAndComponents = context.CompilationProvider.Combine(componentCalls);

        // Generate output
        context.RegisterSourceOutput(compilationAndComponents, Execute);
    }

    private static bool IsAsComponentCall(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Name.Identifier.Text == "AsComponent";
    }

    private static ComponentInfo? GetComponentInfo(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        // Get the generic type argument
        if (memberAccess.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count != 1)
            return null;

        var typeArg = genericName.TypeArgumentList.Arguments[0];
        var typeSymbol = context.SemanticModel.GetTypeInfo(typeArg).Type;

        if (typeSymbol is null)
            return null;

        // Try to find the route path from the MapGet/MapPost call
        var routePath = FindRoutePath(invocation);

        return new ComponentInfo(
            typeSymbol.Name,
            typeSymbol.ContainingNamespace?.ToDisplayString() ?? "",
            routePath ?? "/",
            GetProperties(typeSymbol));
    }

    private static string? FindRoutePath(InvocationExpressionSyntax invocation)
    {
        // Walk up to find MapGet("/path", ...) or similar
        var current = invocation.Parent;
        while (current is not null)
        {
            if (current is InvocationExpressionSyntax parentInvocation &&
                parentInvocation.Expression is MemberAccessExpressionSyntax parentMember &&
                parentMember.Name.Identifier.Text.StartsWith("Map"))
            {
                var args = parentInvocation.ArgumentList.Arguments;
                if (args.Count > 0 &&
                    args[0].Expression is LiteralExpressionSyntax literal)
                {
                    return literal.Token.ValueText;
                }
            }
            current = current.Parent;
        }
        return null;
    }

    private static ImmutableArray<PropertyInfo> GetProperties(ITypeSymbol type)
    {
        var properties = ImmutableArray.CreateBuilder<PropertyInfo>();
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
            {
                properties.Add(new PropertyInfo(
                    prop.Name,
                    GetTypeScriptType(prop.Type)));
            }
        }
        return properties.ToImmutable();
    }

    private static string GetTypeScriptType(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_String => "string",
            SpecialType.System_Int32 or SpecialType.System_Int64 or
            SpecialType.System_Single or SpecialType.System_Double or
            SpecialType.System_Decimal => "number",
            SpecialType.System_Boolean => "boolean",
            _ => type.Name.ToLowerInvariant()
        };
    }

    private static void Execute(
        SourceProductionContext context,
        (Compilation Compilation, ImmutableArray<ComponentInfo?> Components) source)
    {
        var components = source.Components
            .Where(c => c is not null)
            .Cast<ComponentInfo>()
            .ToList();

        // Generate C# routes
        GenerateCSharpRoutes(context, components);

        // Generate TypeScript embedded in C# files (for MSBuild task to extract)
        GenerateTypeScriptTypesEmbedded(context, components);
        GenerateTypeScriptRoutesEmbedded(context, components);
        GenerateTypeScriptRouteDefsEmbedded(context, components);
        GenerateTypeScriptPathsEmbedded(context, components);
    }

    private static void GenerateCSharpRoutes(
        SourceProductionContext context,
        List<ComponentInfo> components)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("namespace Shalimar.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class Routes");
        sb.AppendLine("{");

        foreach (var component in components)
        {
            sb.AppendLine($"    public static string {component.TypeName} => \"{component.Path}\";");
        }

        if (components.Count == 0)
        {
            sb.AppendLine("    // No routes defined yet");
        }

        sb.AppendLine("}");

        context.AddSource("Routes.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateTypeScriptTypesEmbedded(
        SourceProductionContext context,
        List<ComponentInfo> components)
    {
        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine();

        foreach (var component in components)
        {
            tsSb.AppendLine($"export interface {component.TypeName} {{");
            foreach (var prop in component.Properties)
            {
                var camelName = char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1);
                tsSb.AppendLine($"    {camelName}: {prop.TypeScriptType};");
            }
            tsSb.AppendLine("}");
            tsSb.AppendLine();
        }

        if (components.Count == 0)
        {
            tsSb.AppendLine("// No types generated yet - add routes with .AsComponent<T>()");
        }

        // Embed TypeScript in block comment (valid C#)
        var csSb = new StringBuilder();
        csSb.AppendLine("// <auto-generated/>");
        csSb.AppendLine("/*");
        csSb.AppendLine("SHALIMAR_TS: shalimar-types.g.ts");
        csSb.Append(tsSb);
        csSb.AppendLine("END_SHALIMAR_TS");
        csSb.AppendLine("*/");

        context.AddSource("ShalimarTypes.g.cs", SourceText.From(csSb.ToString(), Encoding.UTF8));
    }

    private static void GenerateTypeScriptRoutesEmbedded(
        SourceProductionContext context,
        List<ComponentInfo> components)
    {
        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine("import { rootRoute, index, route } from '@tanstack/virtual-file-routes'");
        tsSb.AppendLine();
        tsSb.AppendLine("export const routes = rootRoute('Client/root.tsx', [");

        // NOTE: We generate TanStack virtual file route configs from ASP.NET route paths.
        // This is intentionally path-driven so we can later generate nested route trees.
        // File mapping convention:
        //   "/"                      -> Features/Home/route.tsx
        //   "/tasks"                 -> Features/Tasks/route.tsx
        //   "/tasks/board"           -> Features/Tasks/Board/route.tsx
        //   "/tasks/{taskId}"        -> Features/Tasks/$taskId/route.tsx
        //   "/accounts/{accountId}"  -> Features/Accounts/$accountId/route.tsx
        // Each segment becomes a folder name (PascalCase) except parameter segments (`{x}`) which become `$x`.

        var routesTree = BuildRoutesTree(components.Select(c => c.Path).ToList());
        EmitVirtualRoutes(tsSb, routesTree, indent: "    ");

        tsSb.AppendLine("])");

        // Embed TypeScript in block comment (valid C#)
        var csSb = new StringBuilder();
        csSb.AppendLine("// <auto-generated/>");
        csSb.AppendLine("/*");
        csSb.AppendLine("SHALIMAR_TS: shalimar-routes.g.ts");
        csSb.Append(tsSb);
        csSb.AppendLine("END_SHALIMAR_TS");
        csSb.AppendLine("*/");

        context.AddSource("ShalimarRoutes.g.cs", SourceText.From(csSb.ToString(), Encoding.UTF8));
    }

    private sealed class RouteNode
    {
        public string Segment { get; }
        public string? FilePath { get; set; }
        public List<RouteNode> Children { get; } = new();

        public RouteNode(string segment)
        {
            Segment = segment;
        }
    }

    private static RouteNode BuildRoutesTree(List<string> paths)
    {
        var root = new RouteNode("");

        // Ensure deterministic ordering.
        foreach (var path in paths.Distinct().OrderBy(p => p, StringComparer.Ordinal))
        {
            var normalized = path == "/" ? "/" : "/" + path.Trim('/'); // normalize
            if (normalized == "/")
            {
                root.FilePath = "Features/Home/route.tsx";
                continue;
            }

            var segments = normalized.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                var child = current.Children.FirstOrDefault(c => c.Segment == seg);
                if (child == null)
                {
                    child = new RouteNode(seg);
                    current.Children.Add(child);
                }
                current = child;
            }

            current.FilePath = MapPathToRouteFile(normalized);
        }

        // Sort children at each node for stable output.
        SortTree(root);
        return root;
    }

    private static void SortTree(RouteNode node)
    {
        node.Children.Sort((a, b) => StringComparer.Ordinal.Compare(a.Segment, b.Segment));
        foreach (var c in node.Children) SortTree(c);
    }

    private static string MapPathToRouteFile(string normalizedPath)
    {
        if (normalizedPath == "/")
            return "Features/Home/route.tsx";

        var parts = normalizedPath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var folders = parts.Select(p =>
        {
            if (p.StartsWith("{") && p.EndsWith("}") && p.Length > 2)
                return "$" + p.Substring(1, p.Length - 2);
            return ToPascalCase(p);
        });

        return $"Features/{string.Join("/", folders)}/route.tsx";
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return s;

        // Preserve existing casing for non-letter prefixes (e.g. '$taskId', though those should be handled earlier).
        var parts = s.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p.Substring(1));
        }
        return sb.ToString();
    }

    private static void EmitVirtualRoutes(StringBuilder tsSb, RouteNode root, string indent)
    {
        // Index route
        if (!string.IsNullOrWhiteSpace(root.FilePath))
        {
            tsSb.AppendLine($"{indent}index('{root.FilePath}'),");
        }

        foreach (var child in root.Children)
        {
            EmitNode(tsSb, child, indent);
        }
    }

    private static void EmitNode(StringBuilder tsSb, RouteNode node, string indent)
    {
        var pathSegment = node.Segment.StartsWith("{") && node.Segment.EndsWith("}") && node.Segment.Length > 2
            ? "$" + node.Segment.Substring(1, node.Segment.Length - 2)
            : node.Segment;

        if (node.Children.Count == 0)
        {
            // Leaf
            var file = node.FilePath ?? $"Features/{ToPascalCase(pathSegment)}/route.tsx";
            tsSb.AppendLine($"{indent}route('{pathSegment}', '{file}'),");
            return;
        }

        var filePath = node.FilePath ?? $"Features/{ToPascalCase(pathSegment)}/route.tsx";
        tsSb.AppendLine($"{indent}route('{pathSegment}', '{filePath}', [");
        foreach (var c in node.Children)
        {
            EmitNode(tsSb, c, indent + "    ");
        }
        tsSb.AppendLine($"{indent}]),");
    }

    private static void GenerateTypeScriptRouteDefsEmbedded(
        SourceProductionContext context,
        List<ComponentInfo> components)
    {
        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine("// Route definitions for type-safe navigation");
        tsSb.AppendLine();
        tsSb.AppendLine("export const routeDefs = {");

        foreach (var component in components)
        {
            var camelName = char.ToLowerInvariant(component.TypeName[0]) + component.TypeName.Substring(1);
            tsSb.AppendLine($"    {camelName}: '{component.Path}',");
        }

        if (components.Count == 0)
        {
            tsSb.AppendLine("    home: '/',");
        }

        tsSb.AppendLine("} as const;");

        // Embed TypeScript in block comment (valid C#)
        var csSb = new StringBuilder();
        csSb.AppendLine("// <auto-generated/>");
        csSb.AppendLine("/*");
        csSb.AppendLine("SHALIMAR_TS: shalimar-route-defs.g.ts");
        csSb.Append(tsSb);
        csSb.AppendLine("END_SHALIMAR_TS");
        csSb.AppendLine("*/");

        context.AddSource("ShalimarRouteDefs.g.cs", SourceText.From(csSb.ToString(), Encoding.UTF8));
    }

    private static void GenerateTypeScriptPathsEmbedded(
        SourceProductionContext context,
        List<ComponentInfo> components)
    {
        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine("// Type-safe path helpers");
        tsSb.AppendLine();
        tsSb.AppendLine("export const paths = {");

        foreach (var component in components)
        {
            var camelName = char.ToLowerInvariant(component.TypeName[0]) + component.TypeName.Substring(1);
            tsSb.AppendLine($"    {camelName}: () => '{component.Path}' as const,");
        }

        if (components.Count == 0)
        {
            tsSb.AppendLine("    home: () => '/' as const,");
        }

        tsSb.AppendLine("};");

        // Embed TypeScript in block comment (valid C#)
        var csSb = new StringBuilder();
        csSb.AppendLine("// <auto-generated/>");
        csSb.AppendLine("/*");
        csSb.AppendLine("SHALIMAR_TS: shalimar-paths.g.ts");
        csSb.Append(tsSb);
        csSb.AppendLine("END_SHALIMAR_TS");
        csSb.AppendLine("*/");

        context.AddSource("ShalimarPaths.g.cs", SourceText.From(csSb.ToString(), Encoding.UTF8));
    }

    // Using classes instead of records for netstandard2.0 compatibility
    private sealed class ComponentInfo
    {
        public string TypeName { get; }
        public string Namespace { get; }
        public string Path { get; }
        public ImmutableArray<PropertyInfo> Properties { get; }

        public ComponentInfo(string typeName, string ns, string path, ImmutableArray<PropertyInfo> properties)
        {
            TypeName = typeName;
            Namespace = ns;
            Path = path;
            Properties = properties;
        }
    }

    private sealed class PropertyInfo
    {
        public string Name { get; }
        public string TypeScriptType { get; }

        public PropertyInfo(string name, string typeScriptType)
        {
            Name = name;
            TypeScriptType = typeScriptType;
        }
    }
}
