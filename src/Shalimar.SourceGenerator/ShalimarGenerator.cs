using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
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
    private static readonly DiagnosticDescriptor V2MissingLeafBinding = new(
        id: "SHALIMARV2001",
        title: "Missing v2 tree endpoint binding",
        messageFormat: "v2 tree leaf '{0}.{1}' ({2}) has no bound endpoint. Add an endpoint annotated with ForComponent<{0}>().ForNode<{0}>(p => p.{1}).As{2}<...>().",
        category: "Shalimar.V2",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all invocations of AsComponent<T>()
        var componentCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsAsComponentCall(node),
                transform: static (ctx, _) => GetComponentInfo(ctx))
            .Where(static info => info is not null)
            .Collect();

        // Find all invocations of AsDeferred<T>()
        var deferredCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsAsDeferredCall(node),
                transform: static (ctx, _) => GetDeferredInfo(ctx))
            .Where(static info => info is not null)
            .Collect();

        // Find all invocations of AsLazy<T>()
        var lazyCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsAsLazyCall(node),
                transform: static (ctx, _) => GetLazyInfo(ctx))
            .Where(static info => info is not null)
            .Collect();

        // Find all invocations of AsStream<T>()
        var streamCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsAsStreamCall(node),
                transform: static (ctx, _) => GetStreamInfo(ctx))
            .Where(static info => info is not null)
            .Collect();

        // Find all invocations of AsSse<T>()
        var sseCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsAsSseCall(node),
                transform: static (ctx, _) => GetSseInfo(ctx))
            .Where(static info => info is not null)
            .Collect();

        // Find all invocations of AsMutation<TReq, TRes>()
        var mutationCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsAsMutationCall(node),
                transform: static (ctx, _) => GetMutationInfo(ctx))
            .Where(static info => info is not null)
            .Collect();

        // Combine with compilation
        var compilationAndComponents = context.CompilationProvider
            .Combine(componentCalls)
            .Combine(deferredCalls)
            .Combine(lazyCalls)
            .Combine(streamCalls)
            .Combine(sseCalls)
            .Combine(mutationCalls);

        // Generate output
        context.RegisterSourceOutput(compilationAndComponents, Execute);
    }

    private static bool IsAsComponentCall(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Name.Identifier.Text == "AsComponent";
    }

    private static bool IsAsDeferredCall(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Name.Identifier.Text == "AsDeferred";
    }

    private static bool IsAsLazyCall(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Name.Identifier.Text == "AsLazy";
    }

    private static bool IsAsStreamCall(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Name.Identifier.Text == "AsStream";
    }

    private static bool IsAsSseCall(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Name.Identifier.Text == "AsSse";
    }

    private static bool IsAsMutationCall(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Name.Identifier.Text == "AsMutation";
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
        var tsxFile = FindForTsxFile(invocation);

        return new ComponentInfo(
            typeSymbol.Name,
            typeSymbol.ContainingNamespace?.ToDisplayString() ?? "",
            routePath ?? "/",
            GetProperties(typeSymbol),
            tsxFile);
    }

    private static DeferredInfo? GetDeferredInfo(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        if (memberAccess.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count != 1)
            return null;

        var typeArg = genericName.TypeArgumentList.Arguments[0];
        var typeSymbol = context.SemanticModel.GetTypeInfo(typeArg).Type;
        if (typeSymbol is null) return null;

        var routePath = FindRoutePath(invocation);
        if (string.IsNullOrWhiteSpace(routePath)) return null;

        var method = MakeRefMethodName(routePath!);
        var parent = FindForComponentTypeName(invocation);
        var nodePath = FindForNodePath(invocation);
        return new DeferredInfo(typeSymbol, routePath!, method, parent, nodePath);
    }

    private static LazyInfo? GetLazyInfo(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        if (memberAccess.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count != 1)
            return null;

        var typeArg = genericName.TypeArgumentList.Arguments[0];
        var typeSymbol = context.SemanticModel.GetTypeInfo(typeArg).Type;
        if (typeSymbol is null) return null;

        var routePath = FindRoutePath(invocation);
        if (string.IsNullOrWhiteSpace(routePath)) return null;

        var method = MakeRefMethodName(routePath!);
        var parent = FindForComponentTypeName(invocation);
        var nodePath = FindForNodePath(invocation);
        return new LazyInfo(typeSymbol, routePath!, method, parent, nodePath);
    }

    private static StreamInfo? GetStreamInfo(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        if (memberAccess.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count != 1)
            return null;

        var typeArg = genericName.TypeArgumentList.Arguments[0];
        var typeSymbol = context.SemanticModel.GetTypeInfo(typeArg).Type;
        if (typeSymbol is null) return null;

        var routePath = FindRoutePath(invocation);
        if (string.IsNullOrWhiteSpace(routePath)) return null;

        var method = MakeRefMethodName(routePath!);
        var parent = FindForComponentTypeName(invocation);
        var nodePath = FindForNodePath(invocation);
        return new StreamInfo(typeSymbol, routePath!, method, parent, nodePath);
    }

    private static SseInfo? GetSseInfo(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        if (memberAccess.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count != 1)
            return null;

        var typeArg = genericName.TypeArgumentList.Arguments[0];
        var typeSymbol = context.SemanticModel.GetTypeInfo(typeArg).Type;
        if (typeSymbol is null) return null;

        var routePath = FindRoutePath(invocation);
        if (string.IsNullOrWhiteSpace(routePath)) return null;

        var method = MakeRefMethodName(routePath!);
        var parent = FindForComponentTypeName(invocation);
        var nodePath = FindForNodePath(invocation);
        return new SseInfo(typeSymbol, routePath!, method, parent, nodePath);
    }

    private static string? FindForTsxFile(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return null;

        ExpressionSyntax? current = access.Expression;
        while (current is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma)
            {
                if (ma.Name is IdentifierNameSyntax id && id.Identifier.Text == "ForTsxFile")
                {
                    if (inv.ArgumentList.Arguments.Count == 1 &&
                        inv.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax lit &&
                        lit.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        return lit.Token.ValueText;
                    }
                }
                current = ma.Expression;
                continue;
            }
            break;
        }

        return null;
    }

    private static string? FindForNodePath(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return null;

        ExpressionSyntax? current = access.Expression;
        while (current is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma)
            {
                // Look for .ForNode<TProps>(p => p.X.Y.Props.Z)
                if (ma.Name is GenericNameSyntax gn && gn.Identifier.Text == "ForNode")
                {
                    if (inv.ArgumentList.Arguments.Count == 1)
                    {
                        var expr = inv.ArgumentList.Arguments[0].Expression;
                        var path = ExtractMemberPathFromLambda(expr);
                        if (!string.IsNullOrWhiteSpace(path))
                            return path;
                    }
                }
                current = ma.Expression;
                continue;
            }
            break;
        }

        return null;
    }

    private static string? ExtractMemberPathFromLambda(ExpressionSyntax expr)
    {
        // p => p.A.B.Props.C  (drop ".Props")
        ExpressionSyntax body = expr;
        if (body is ParenthesizedLambdaExpressionSyntax pl && pl.ExpressionBody is not null)
            body = pl.ExpressionBody;
        if (body is SimpleLambdaExpressionSyntax sl && sl.ExpressionBody is not null)
            body = sl.ExpressionBody;

        // strip casts
        while (body is CastExpressionSyntax cast)
            body = cast.Expression;

        var segments = new Stack<string>();
        while (body is MemberAccessExpressionSyntax ma)
        {
            var name = ma.Name.Identifier.ValueText;
            if (!string.Equals(name, "Props", StringComparison.Ordinal))
                segments.Push(name);
            body = ma.Expression;
        }

        return segments.Count == 0 ? null : string.Join(".", segments);
    }

    private static MutationInfo? GetMutationInfo(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        if (memberAccess.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count != 2)
            return null;

        var reqArg = genericName.TypeArgumentList.Arguments[0];
        var resArg = genericName.TypeArgumentList.Arguments[1];

        var reqType = context.SemanticModel.GetTypeInfo(reqArg).Type;
        var resType = context.SemanticModel.GetTypeInfo(resArg).Type;
        if (reqType is null || resType is null) return null;

        var routePath = FindRoutePath(invocation);
        if (string.IsNullOrWhiteSpace(routePath)) return null;

        var methodName = MakeRefMethodName(routePath!);
        var httpMethod = FindHttpMethod(invocation) ?? "POST";
        var invalidates = FindInvalidatesTypeNames(invocation);

        return new MutationInfo(reqType, resType, routePath!, methodName, httpMethod, invalidates);
    }

    private static string? FindHttpMethod(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return null;

        ExpressionSyntax? current = access.Expression;
        while (current is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma)
            {
                var name = ma.Name.Identifier.Text;
                if (name.StartsWith("Map", StringComparison.Ordinal))
                {
                    if (name.Equals("MapPost", StringComparison.Ordinal)) return "POST";
                    if (name.Equals("MapPut", StringComparison.Ordinal)) return "PUT";
                    if (name.Equals("MapPatch", StringComparison.Ordinal)) return "PATCH";
                    if (name.Equals("MapDelete", StringComparison.Ordinal)) return "DELETE";
                    return null;
                }
                current = ma.Expression;
                continue;
            }
            break;
        }
        return null;
    }

    private static List<string> FindInvalidatesTypeNames(InvocationExpressionSyntax invocation)
    {
        var output = new List<string>();
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return output;

        ExpressionSyntax? current = access.Expression;
        while (current is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma)
            {
                if (ma.Name is GenericNameSyntax gn && gn.Identifier.Text == "Invalidates" && gn.TypeArgumentList.Arguments.Count == 1)
                {
                    output.Add(gn.TypeArgumentList.Arguments[0].ToString());
                }
                current = ma.Expression;
                continue;
            }
            break;
        }

        output.Sort(StringComparer.Ordinal);
        return output;
    }

    private static string? FindRoutePath(InvocationExpressionSyntax invocation)
    {
        // `invocation` is the `.AsComponent<T>()` call. The route path lives on the
        // receiver chain, e.g.:
        //   app.MapGet("/tasks", ...).WithName("...").AsComponent<TProps>()
        //
        // We walk left through the fluent chain until we hit a `Map*` call.
        if (invocation.Expression is not MemberAccessExpressionSyntax asComponentAccess)
            return null;

        ExpressionSyntax? current = asComponentAccess.Expression;
        while (current is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma)
            {
                var name = ma.Name.Identifier.Text;
                if (name.StartsWith("Map", StringComparison.Ordinal))
                {
                    var args = inv.ArgumentList.Arguments;
                    if (args.Count > 0 && args[0].Expression is LiteralExpressionSyntax literal)
                        return literal.Token.ValueText;
                    return null;
                }

                current = ma.Expression;
                continue;
            }

            break;
        }

        return null;
    }

    private static string MakeRefMethodName(string routePath)
    {
        var normalized = routePath == "/" ? "/" : "/" + routePath.Trim('/');
        if (normalized == "/") return "Home";

        var segments = normalized.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var seg in segments)
        {
            if (seg.StartsWith("{") && seg.EndsWith("}") && seg.Length > 2)
            {
                sb.Append("By");
                sb.Append(ToPascalCase(seg.Substring(1, seg.Length - 2)));
                continue;
            }
            sb.Append(ToPascalCase(seg));
        }
        return sb.Length == 0 ? "Deferred" : sb.ToString();
    }

    private static string? FindForComponentTypeName(InvocationExpressionSyntax invocation)
    {
        // `invocation` is the terminal `.AsX<T>()` call. We walk left through the fluent chain
        // to find `.ForComponent<TProps>()` if present.
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return null;

        ExpressionSyntax? current = access.Expression;
        while (current is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma)
            {
                if (ma.Name is GenericNameSyntax gn && gn.Identifier.Text == "ForComponent" && gn.TypeArgumentList.Arguments.Count == 1)
                {
                    return gn.TypeArgumentList.Arguments[0].ToString();
                }
                current = ma.Expression;
                continue;
            }
            break;
        }

        return null;
    }

    private static string SanitizeIdentifier(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
        }
        if (sb.Length == 0 || (!char.IsLetter(sb[0]) && sb[0] != '_')) sb.Insert(0, '_');
        return sb.ToString();
    }

    private static ImmutableArray<PropertyInfo> GetProperties(ITypeSymbol type)
    {
        var properties = ImmutableArray.CreateBuilder<PropertyInfo>();
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
            {
                properties.Add(new PropertyInfo(prop.Name, prop.Type));
            }
        }
        return properties.ToImmutable();
    }

    private static void Execute(
        SourceProductionContext context,
        ((((((Compilation Compilation, ImmutableArray<ComponentInfo?> Components) Left, ImmutableArray<DeferredInfo?> Deferred) Mid, ImmutableArray<LazyInfo?> Lazy) Left2, ImmutableArray<StreamInfo?> Stream) Left3, ImmutableArray<SseInfo?> Sse) Left4, ImmutableArray<MutationInfo?> Mutations) source)
    {
        var compilation = source.Left4.Left3.Left2.Mid.Left.Compilation;
        var components = source.Left4.Left3.Left2.Mid.Left.Components
            .Where(c => c is not null)
            .Cast<ComponentInfo>()
            .ToList();

        var deferred = source.Left4.Left3.Left2.Mid.Deferred
            .Where(d => d is not null)
            .Cast<DeferredInfo>()
            .GroupBy(d => d.Path, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(d => d.Path, StringComparer.Ordinal)
            .ToList();

        var lazy = source.Left4.Left3.Left2.Lazy
            .Where(d => d is not null)
            .Cast<LazyInfo>()
            .GroupBy(d => d.Path, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(d => d.Path, StringComparer.Ordinal)
            .ToList();

        var stream = source.Left4.Left3.Stream
            .Where(d => d is not null)
            .Cast<StreamInfo>()
            .GroupBy(d => d.Path, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(d => d.Path, StringComparer.Ordinal)
            .ToList();

        var sse = source.Left4.Sse
            .Where(d => d is not null)
            .Cast<SseInfo>()
            .GroupBy(d => d.Path, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(d => d.Path, StringComparer.Ordinal)
            .ToList();

        var mutations = source.Mutations
            .Where(m => m is not null)
            .Cast<MutationInfo>()
            .GroupBy(m => m.Path, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(m => m.Path, StringComparer.Ordinal)
            .ToList();

        // Generate C# routes
        GenerateCSharpRoutes(context, components);
        GenerateCSharpDeferredRefs(context, deferred);
        GenerateCSharpLazyRefs(context, lazy);
        GenerateCSharpStreamRefs(context, stream);
        GenerateCSharpSseRefs(context, sse);
        GenerateCSharpComponentTreeRefs(context, deferred, lazy, stream, sse);
        GenerateCSharpBehaviorsRefs(context, deferred, lazy, stream, sse);

        ValidateV2TreeBindings(context, compilation, components, deferred, lazy, stream, sse);

        GenerateTypeScriptInvalidationsEmbedded(context, compilation, deferred, lazy, stream, sse);
        GenerateTypeScriptMutationsEmbedded(context, compilation, mutations);

        // Generate TypeScript embedded in C# files (for MSBuild task to extract)
        GenerateTypeScriptTypesEmbedded(context, compilation, components, mutations);
        GenerateTypeScriptZodSchemasEmbedded(context, compilation, mutations);
        GenerateTypeScriptDefaultsEmbedded(context, compilation, mutations);
        GenerateTypeScriptUseMutationsEmbedded(context, compilation, mutations);
        GenerateTypeScriptRoutesEmbedded(context, components);
        GenerateTypeScriptRouteDefsEmbedded(context, components);
        GenerateTypeScriptPathsEmbedded(context, components);
        GenerateTypeScriptFacadesEmbedded(context);
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

        sb.AppendLine();
        sb.AppendLine("    // Typed route helpers (derived from Minimal API route templates)");
        foreach (var component in components.OrderBy(c => c.TypeName, StringComparer.Ordinal))
        {
            var template = component.Path;
            if (string.IsNullOrWhiteSpace(template)) continue;
            if (template.IndexOf("{", StringComparison.Ordinal) < 0) continue;

            var paramSpecs = ExtractRouteParamsWithConstraints(template);
            if (paramSpecs.Count == 0) continue;

            var methodName = $"{component.TypeName}Path";
            var sig = string.Join(", ", paramSpecs.Select(p => $"{p.CsType} {p.ParamName}"));
            var expr = BuildCSharpPathExpression(template, paramSpecs);
            sb.AppendLine($"    public static string {methodName}({sig}) => {expr};");
        }

        if (components.Count == 0)
        {
            sb.AppendLine("    // No routes defined yet");
        }

        sb.AppendLine("}");

        context.AddSource("Routes.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private enum ModeKind
    {
        Deferred,
        Lazy,
        Stream,
        Sse
    }

    private sealed class LeafRequirement
    {
        public INamedTypeSymbol PropsType { get; }
        public string NodePath { get; }
        public ModeKind Mode { get; }

        public LeafRequirement(INamedTypeSymbol propsType, string nodePath, ModeKind mode)
        {
            PropsType = propsType;
            NodePath = nodePath;
            Mode = mode;
        }
    }

    private static void ValidateV2TreeBindings(
        SourceProductionContext context,
        Compilation compilation,
        List<ComponentInfo> components,
        List<DeferredInfo> deferred,
        List<LazyInfo> lazy,
        List<StreamInfo> stream,
        List<SseInfo> sse)
    {
        // v2 validation is opt-in: only enforce for component routes explicitly bound to Features/V2/**
        var v2Roots = components
            .Where(c => !string.IsNullOrWhiteSpace(c.TsxFile) && c.TsxFile!.Replace('\\', '/').StartsWith("Features/V2/", StringComparison.Ordinal))
            .ToList();

        if (v2Roots.Count == 0) return;

        var deferredDef = compilation.GetTypeByMetadataName("Shalimar.Deferred`1");
        var lazyDef = compilation.GetTypeByMetadataName("Shalimar.Lazy`1");
        var streamDef = compilation.GetTypeByMetadataName("Shalimar.Stream`1");
        var sseDef = compilation.GetTypeByMetadataName("Shalimar.Sse`1");
        var componentDef = compilation.GetTypeByMetadataName("Shalimar.Component`1");

        foreach (var root in v2Roots)
        {
            var props = compilation.GetTypeByMetadataName($"{root.Namespace}.{root.TypeName}");
            if (props is null) continue;

            var leaves = new List<LeafRequirement>();
            CollectLeaves(props, prefix: "", leaves);

            foreach (var leaf in leaves)
            {
                var propsName = leaf.PropsType.Name;
                var path = leaf.NodePath;

                var satisfied = leaf.Mode switch
                {
                    ModeKind.Deferred => deferred.Any(d => d.ParentComponentTypeName == propsName && d.NodePath == path),
                    ModeKind.Lazy => lazy.Any(d => d.ParentComponentTypeName == propsName && d.NodePath == path),
                    ModeKind.Stream => stream.Any(d => d.ParentComponentTypeName == propsName && d.NodePath == path),
                    ModeKind.Sse => sse.Any(d => d.ParentComponentTypeName == propsName && d.NodePath == path),
                    _ => false
                };

                if (!satisfied)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        V2MissingLeafBinding,
                        Location.None,
                        propsName,
                        path,
                        leaf.Mode.ToString()));
                }
            }
        }

        void CollectLeaves(INamedTypeSymbol propsType, string prefix, List<LeafRequirement> output)
        {
            foreach (var p in propsType.GetMembers().OfType<IPropertySymbol>())
            {
                if (p.DeclaredAccessibility != Accessibility.Public || p.IsStatic) continue;

                var name = p.Name;
                var nextPrefix = string.IsNullOrEmpty(prefix) ? name : prefix + "." + name;

                if (p.Type is INamedTypeSymbol named && named.IsGenericType)
                {
                    var def = named.ConstructedFrom;

                    if (componentDef is not null && SymbolEqualityComparer.Default.Equals(def, componentDef))
                    {
                        if (named.TypeArguments.Length == 1 && named.TypeArguments[0] is INamedTypeSymbol childProps)
                            CollectLeaves(childProps, prefix: nextPrefix, output);
                        continue;
                    }

                    if (deferredDef is not null && SymbolEqualityComparer.Default.Equals(def, deferredDef))
                        output.Add(new LeafRequirement(propsType, nextPrefix, ModeKind.Deferred));
                    else if (lazyDef is not null && SymbolEqualityComparer.Default.Equals(def, lazyDef))
                        output.Add(new LeafRequirement(propsType, nextPrefix, ModeKind.Lazy));
                    else if (streamDef is not null && SymbolEqualityComparer.Default.Equals(def, streamDef))
                        output.Add(new LeafRequirement(propsType, nextPrefix, ModeKind.Stream));
                    else if (sseDef is not null && SymbolEqualityComparer.Default.Equals(def, sseDef))
                        output.Add(new LeafRequirement(propsType, nextPrefix, ModeKind.Sse));
                }
            }
        }
    }

    private sealed class RouteParamSpec
    {
        public string ParamName { get; }
        public string CsType { get; }
        public string Placeholder { get; }

        public RouteParamSpec(string paramName, string csType, string placeholder)
        {
            ParamName = paramName;
            CsType = csType;
            Placeholder = placeholder;
        }
    }

    private static List<RouteParamSpec> ExtractRouteParamsWithConstraints(string template)
    {
        var output = new List<RouteParamSpec>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in ExtractRouteParams(template))
        {
            // raw may be: "id", "id:int", "id:guid", "id:int:min(1)", "id?"
            var namePart = raw;
            var colonIdx = raw.IndexOf(':');
            if (colonIdx >= 0) namePart = raw.Substring(0, colonIdx);

            // Optional segments are not supported yet in typed helpers (avoid lying about required params).
            if (namePart.EndsWith("?", StringComparison.Ordinal))
                continue;

            var paramName = SanitizeIdentifier(namePart);
            if (!names.Add(paramName)) continue;

            var constraint = colonIdx >= 0 ? raw.Substring(colonIdx + 1) : null;
            var csType = MapRouteConstraintToCSharpType(constraint);
            output.Add(new RouteParamSpec(paramName, csType, namePart));
        }

        return output;
    }

    private static string MapRouteConstraintToCSharpType(string? constraint)
    {
        if (string.IsNullOrWhiteSpace(constraint))
            return "string";

        // Use the first constraint token (e.g. "int:min(1)" => "int").
        var first = constraint!.Split(':')[0].Trim();
        return first switch
        {
            "int" => "global::System.Int32",
            "long" => "global::System.Int64",
            "guid" => "global::System.Guid",
            "bool" => "global::System.Boolean",
            _ => "string"
        };
    }

    private static string BuildCSharpPathExpression(string template, List<RouteParamSpec> specs)
    {
        // Build: "/a/" + Uri.EscapeDataString(x.ToString(...)) + "/b"
        var normalized = template == "/" ? "/" : "/" + template.Trim('/');
        if (normalized == "/") return "\"/\"";

        var segments = normalized.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string> { "\"/\"" };

        foreach (var seg in segments)
        {
            if (seg.StartsWith("{") && seg.EndsWith("}") && seg.Length > 2)
            {
                var inner = seg.Substring(1, seg.Length - 2);
                var namePart = inner;
                var colonIdx = inner.IndexOf(':');
                if (colonIdx >= 0) namePart = inner.Substring(0, colonIdx);
                if (namePart.EndsWith("?", StringComparison.Ordinal))
                    throw new InvalidOperationException("Optional route params not supported in typed route helpers.");

                var paramName = SanitizeIdentifier(namePart);
                var spec = specs.FirstOrDefault(s => s.ParamName == paramName);
                if (spec is null) continue;

                var toStringExpr = spec.CsType switch
                {
                    "global::System.Int32" or "global::System.Int64" =>
                        $"{paramName}.ToString(global::System.Globalization.CultureInfo.InvariantCulture)",
                    _ => $"{paramName}.ToString()"
                };
                parts.Add($"global::System.Uri.EscapeDataString({toStringExpr})");
            }
            else
            {
                parts.Add($"\"{seg}\"");
            }
        }

        // Join with "/"
        // "/"+seg0+"/"+seg1...
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            if (i == 0)
            {
                sb.Append(parts[i]);
                continue;
            }

            sb.Append(" + \"/\" + ");
            sb.Append(parts[i]);
        }

        return sb.ToString();
    }

    private static void GenerateCSharpDeferredRefs(SourceProductionContext context, List<DeferredInfo> deferred)
    {
        if (deferred.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("namespace Shalimar.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class DeferredRefs");
        sb.AppendLine("{");

        foreach (var d in deferred)
        {
            var ts = d.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"    public static global::Shalimar.Deferred<{ts}> {d.RefMethodName}() => new global::Shalimar.Deferred<{ts}>(\"{d.Path}\");");
        }

        sb.AppendLine("}");
        context.AddSource("DeferredRefs.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateCSharpLazyRefs(SourceProductionContext context, List<LazyInfo> lazy)
    {
        if (lazy.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("namespace Shalimar.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class LazyRefs");
        sb.AppendLine("{");

        foreach (var d in lazy)
        {
            var ts = d.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"    public static global::Shalimar.Lazy<{ts}> {d.RefMethodName}() => new global::Shalimar.Lazy<{ts}>(\"{d.Path}\");");
        }

        sb.AppendLine("}");
        context.AddSource("LazyRefs.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateCSharpStreamRefs(SourceProductionContext context, List<StreamInfo> stream)
    {
        if (stream.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("namespace Shalimar.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class StreamRefs");
        sb.AppendLine("{");

        foreach (var d in stream)
        {
            var ts = d.EventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"    public static global::Shalimar.Stream<{ts}> {d.RefMethodName}() => new global::Shalimar.Stream<{ts}>(\"{d.Path}\");");
        }

        sb.AppendLine("}");
        context.AddSource("StreamRefs.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateCSharpSseRefs(SourceProductionContext context, List<SseInfo> sse)
    {
        if (sse.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("namespace Shalimar.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class SseRefs");
        sb.AppendLine("{");

        foreach (var d in sse)
        {
            var ts = d.EventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"    public static global::Shalimar.Sse<{ts}> {d.RefMethodName}() => new global::Shalimar.Sse<{ts}>(\"{d.Path}\");");
        }

        sb.AppendLine("}");
        context.AddSource("SseRefs.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateCSharpComponentTreeRefs(
        SourceProductionContext context,
        List<DeferredInfo> deferred,
        List<LazyInfo> lazy,
        List<StreamInfo> stream,
        List<SseInfo> sse)
    {
        var any = deferred.Any(d => !string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) ||
                  lazy.Any(d => !string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) ||
                  stream.Any(d => !string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) ||
                  sse.Any(d => !string.IsNullOrWhiteSpace(d.ParentComponentTypeName));

        if (!any) return;

        static IEnumerable<(string Parent, string Mode, string Method, string ReturnType)> CollectDeferred(IEnumerable<DeferredInfo> items)
        {
            foreach (var i in items)
            {
                if (string.IsNullOrWhiteSpace(i.ParentComponentTypeName)) continue;
                var rt = $"global::Shalimar.Deferred<{i.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";
                yield return (i.ParentComponentTypeName!, "Deferred", i.RefMethodName, rt);
            }
        }

        static IEnumerable<(string Parent, string Mode, string Method, string ReturnType)> CollectLazy(IEnumerable<LazyInfo> items)
        {
            foreach (var i in items)
            {
                if (string.IsNullOrWhiteSpace(i.ParentComponentTypeName)) continue;
                var rt = $"global::Shalimar.Lazy<{i.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";
                yield return (i.ParentComponentTypeName!, "Lazy", i.RefMethodName, rt);
            }
        }

        static IEnumerable<(string Parent, string Mode, string Method, string ReturnType)> CollectStream(IEnumerable<StreamInfo> items)
        {
            foreach (var i in items)
            {
                if (string.IsNullOrWhiteSpace(i.ParentComponentTypeName)) continue;
                var rt = $"global::Shalimar.Stream<{i.EventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";
                yield return (i.ParentComponentTypeName!, "Stream", i.RefMethodName, rt);
            }
        }

        static IEnumerable<(string Parent, string Mode, string Method, string ReturnType)> CollectSse(IEnumerable<SseInfo> items)
        {
            foreach (var i in items)
            {
                if (string.IsNullOrWhiteSpace(i.ParentComponentTypeName)) continue;
                var rt = $"global::Shalimar.Sse<{i.EventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";
                yield return (i.ParentComponentTypeName!, "Sse", i.RefMethodName, rt);
            }
        }

        var entries = new List<(string Parent, string Mode, string Method, string ReturnType)>();
        entries.AddRange(CollectDeferred(deferred));
        entries.AddRange(CollectLazy(lazy));
        entries.AddRange(CollectStream(stream));
        entries.AddRange(CollectSse(sse));

        var byParent = entries
            .GroupBy(e => e.Parent, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("namespace Shalimar.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class Components");
        sb.AppendLine("{");

        foreach (var parent in byParent)
        {
            var parentName = SanitizeIdentifier(parent.Key);
            sb.AppendLine($"    public static class {parentName}");
            sb.AppendLine("    {");

            foreach (var modeGroup in parent.GroupBy(e => e.Mode, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"        public static class {modeGroup.Key}");
                sb.AppendLine("        {");
                foreach (var e in modeGroup.OrderBy(x => x.Method, StringComparer.Ordinal))
                {
                    // Delegate to the global refs (still generated).
                    var target = e.Mode switch
                    {
                        "Deferred" => $"global::Shalimar.Generated.DeferredRefs.{e.Method}()",
                        "Lazy" => $"global::Shalimar.Generated.LazyRefs.{e.Method}()",
                        "Stream" => $"global::Shalimar.Generated.StreamRefs.{e.Method}()",
                        "Sse" => $"global::Shalimar.Generated.SseRefs.{e.Method}()",
                        _ => throw new InvalidOperationException("Unknown mode")
                    };
                    sb.AppendLine($"            public static {e.ReturnType} {e.Method}() => {target};");
                }
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
        }
        sb.AppendLine("}");

        context.AddSource("Components.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateCSharpBehaviorsRefs(
        SourceProductionContext context,
        List<DeferredInfo> deferred,
        List<LazyInfo> lazy,
        List<StreamInfo> stream,
        List<SseInfo> sse)
    {
        var any = deferred.Any(d => !string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) ||
                  lazy.Any(d => !string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) ||
                  stream.Any(d => !string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) ||
                  sse.Any(d => !string.IsNullOrWhiteSpace(d.ParentComponentTypeName));

        if (!any) return;

        var byParent = new SortedDictionary<string, (SortedSet<string> Deferred, SortedSet<string> Lazy, SortedSet<string> Stream, SortedSet<string> Sse)>(StringComparer.Ordinal);

        static void Ensure(
            SortedDictionary<string, (SortedSet<string> Deferred, SortedSet<string> Lazy, SortedSet<string> Stream, SortedSet<string> Sse)> map,
            string key)
        {
            if (map.ContainsKey(key)) return;
            map[key] = (new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal));
        }

        foreach (var d in deferred)
        {
            if (string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) continue;
            Ensure(byParent, d.ParentComponentTypeName!);
            byParent[d.ParentComponentTypeName!].Deferred.Add(d.Path);
        }
        foreach (var d in lazy)
        {
            if (string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) continue;
            Ensure(byParent, d.ParentComponentTypeName!);
            byParent[d.ParentComponentTypeName!].Lazy.Add(d.Path);
        }
        foreach (var d in stream)
        {
            if (string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) continue;
            Ensure(byParent, d.ParentComponentTypeName!);
            byParent[d.ParentComponentTypeName!].Stream.Add(d.Path);
        }
        foreach (var d in sse)
        {
            if (string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) continue;
            Ensure(byParent, d.ParentComponentTypeName!);
            byParent[d.ParentComponentTypeName!].Sse.Add(d.Path);
        }

        static string EmitArray(SortedSet<string> items) =>
            items.Count == 0 ? "global::System.Array.Empty<string>()" : $"new[] {{ {string.Join(", ", items.Select(i => $"\"{i}\""))} }}";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("namespace Shalimar.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class Behaviors");
        sb.AppendLine("{");

        foreach (var kvp in byParent)
        {
            var parentName = SanitizeIdentifier(kvp.Key);
            sb.AppendLine($"    public static class {parentName}");
            sb.AppendLine("    {");

            sb.AppendLine("        public static global::Shalimar.ShalimarBehaviors PrefetchDeferred() => new(");
            sb.AppendLine($"            DeferredHrefs: {EmitArray(kvp.Value.Deferred)},");
            sb.AppendLine("            LazyHrefs: global::System.Array.Empty<string>(),");
            sb.AppendLine("            StreamedHrefs: global::System.Array.Empty<string>(),");
            sb.AppendLine("            SseHrefs: global::System.Array.Empty<string>());");
            sb.AppendLine();

            sb.AppendLine("        public static global::Shalimar.ShalimarBehaviors All() => new(");
            sb.AppendLine($"            DeferredHrefs: {EmitArray(kvp.Value.Deferred)},");
            sb.AppendLine($"            LazyHrefs: {EmitArray(kvp.Value.Lazy)},");
            sb.AppendLine($"            StreamedHrefs: {EmitArray(kvp.Value.Stream)},");
            sb.AppendLine($"            SseHrefs: {EmitArray(kvp.Value.Sse)});");

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        context.AddSource("Behaviors.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateTypeScriptInvalidationsEmbedded(
        SourceProductionContext context,
        Compilation compilation,
        List<DeferredInfo> deferred,
        List<LazyInfo> lazy,
        List<StreamInfo> stream,
        List<SseInfo> sse)
    {
        // Group hrefs by component (TProps) so invalidations stay explicit and typed.
        var byComponent = new SortedDictionary<string, (SortedSet<string> Deferred, SortedSet<string> Lazy, SortedSet<string> Stream, SortedSet<string> Sse)>(StringComparer.Ordinal);

        static void Ensure(
            SortedDictionary<string, (SortedSet<string> Deferred, SortedSet<string> Lazy, SortedSet<string> Stream, SortedSet<string> Sse)> map,
            string key)
        {
            if (map.ContainsKey(key)) return;
            map[key] = (new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal));
        }

        foreach (var d in deferred)
        {
            if (string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) continue;
            var key = d.ParentComponentTypeName!;
            Ensure(byComponent, key);
            byComponent[key].Deferred.Add(d.Path);
        }
        foreach (var d in lazy)
        {
            if (string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) continue;
            var key = d.ParentComponentTypeName!;
            Ensure(byComponent, key);
            byComponent[key].Lazy.Add(d.Path);
        }
        foreach (var d in stream)
        {
            if (string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) continue;
            var key = d.ParentComponentTypeName!;
            Ensure(byComponent, key);
            byComponent[key].Stream.Add(d.Path);
        }
        foreach (var d in sse)
        {
            if (string.IsNullOrWhiteSpace(d.ParentComponentTypeName)) continue;
            var key = d.ParentComponentTypeName!;
            Ensure(byComponent, key);
            byComponent[key].Sse.Add(d.Path);
        }

        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine("import { invalidateDeferredByPrefix, invalidateLazyByPrefix, invalidateStreamedByPrefix, invalidateSseByPrefix } from '@shalimar/runtime'");
        tsSb.AppendLine();
        tsSb.AppendLine("export const componentInvalidations = {");

        foreach (var kvp in byComponent)
        {
            static string EmitStringArray(IEnumerable<string> items) => "[" + string.Join(", ", items.Select(i => $"'{i}'")) + "]";

            tsSb.AppendLine($"    '{kvp.Key}': {{");
            tsSb.AppendLine($"        deferredHrefs: {EmitStringArray(kvp.Value.Deferred)},");
            tsSb.AppendLine($"        lazyHrefs: {EmitStringArray(kvp.Value.Lazy)},");
            tsSb.AppendLine($"        streamedHrefs: {EmitStringArray(kvp.Value.Stream)},");
            tsSb.AppendLine($"        sseHrefs: {EmitStringArray(kvp.Value.Sse)},");
            tsSb.AppendLine("    },");
        }

        if (byComponent.Count == 0)
        {
            tsSb.AppendLine("    // No component-scoped data-mode endpoints were annotated with .ForComponent<TProps>() yet.");
        }

        tsSb.AppendLine("} as const");
        tsSb.AppendLine();
        tsSb.AppendLine("export type ComponentKey = keyof typeof componentInvalidations");
        tsSb.AppendLine();
        tsSb.AppendLine("export function invalidateComponent(component: ComponentKey) {");
        tsSb.AppendLine("    const inv = componentInvalidations[component]");
        tsSb.AppendLine("    for (const href of inv.deferredHrefs) invalidateDeferredByPrefix(href)");
        tsSb.AppendLine("    for (const href of inv.lazyHrefs) invalidateLazyByPrefix(href)");
        tsSb.AppendLine("    for (const href of inv.streamedHrefs) invalidateStreamedByPrefix(href)");
        tsSb.AppendLine("    for (const href of inv.sseHrefs) invalidateSseByPrefix(href)");
        tsSb.AppendLine("}");
        tsSb.AppendLine();

        // Embed TypeScript in block comment (valid C#)
        var csSb = new StringBuilder();
        csSb.AppendLine("// <auto-generated/>");
        csSb.AppendLine("/*");
        csSb.AppendLine("SHALIMAR_TS: shalimar-invalidations.g.ts");
        csSb.Append(tsSb);
        csSb.AppendLine("END_SHALIMAR_TS");
        csSb.AppendLine("*/");

        context.AddSource("ShalimarInvalidations.g.cs", SourceText.From(csSb.ToString(), Encoding.UTF8));
    }

    private static IReadOnlyList<string> ExtractRouteParams(string path)
    {
        var output = new List<string>();
        if (string.IsNullOrWhiteSpace(path)) return output;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] != '{') continue;
            var end = path.IndexOf('}', i + 1);
            if (end < 0) break;
            var name = path.Substring(i + 1, end - i - 1);
            if (!string.IsNullOrWhiteSpace(name))
                output.Add(name);
            i = end;
        }
        return output;
    }

    private static void GenerateTypeScriptMutationsEmbedded(
        SourceProductionContext context,
        Compilation compilation,
        List<MutationInfo> mutations)
    {
        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine("import { invalidateComponent, type ComponentKey } from './shalimar-invalidations.g'");
        tsSb.AppendLine("import type {");

        // Ensure we import the request/response types we actually reference.
        var typeNames = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var m in mutations)
        {
            typeNames.Add(m.RequestType.Name);
            typeNames.Add(m.ResponseType.Name);
        }
        foreach (var tn in typeNames)
            tsSb.AppendLine($"    {tn},");

        tsSb.AppendLine("} from './shalimar-types.g'");
        tsSb.AppendLine();

        tsSb.AppendLine("export type ValidationErrors = Record<string, string[]>");
        tsSb.AppendLine("export type MutationResult<T> =");
        tsSb.AppendLine("    | { ok: true; value: T }");
        tsSb.AppendLine("    | { ok: false; validation: ValidationErrors }");
        tsSb.AppendLine("    | { ok: false; error: string }");
        tsSb.AppendLine();

        tsSb.AppendLine("async function jsonMutation<TReq, TRes>(url: string, method: string, body: TReq): Promise<MutationResult<TRes>> {");
        tsSb.AppendLine("    const res = await fetch(url, {");
        tsSb.AppendLine("        method,");
        tsSb.AppendLine("        headers: {");
        tsSb.AppendLine("            accept: 'application/json',");
        tsSb.AppendLine("            'content-type': 'application/json',");
        tsSb.AppendLine("        },");
        tsSb.AppendLine("        body: JSON.stringify(body),");
        tsSb.AppendLine("    })");
        tsSb.AppendLine("    const contentType = res.headers.get('content-type') ?? ''");
        tsSb.AppendLine("    if (res.ok) {");
        tsSb.AppendLine("        // Allow command endpoints that intentionally return no content.");
        tsSb.AppendLine("        if (res.status === 204) return { ok: true, value: (undefined as unknown as TRes) }");
        tsSb.AppendLine("        const json = (await res.json().catch(() => null)) as TRes | null");
        tsSb.AppendLine("        if (json === null) return { ok: false, error: `Mutation returned invalid JSON: ${method} ${url}` }");
        tsSb.AppendLine("        return { ok: true, value: json }");
        tsSb.AppendLine("    }");
        tsSb.AppendLine();
        tsSb.AppendLine("    // FluentValidation via Results.ValidationProblem (ProblemDetails with `errors`).");
        tsSb.AppendLine("    if (res.status === 400 && contentType.includes('application/problem+json')) {");
        tsSb.AppendLine("        const problem = (await res.json().catch(() => null)) as any");
        tsSb.AppendLine("        const errors = problem?.errors");
        tsSb.AppendLine("        if (errors && typeof errors === 'object') {");
        tsSb.AppendLine("            return { ok: false, validation: errors as ValidationErrors }");
        tsSb.AppendLine("        }");
        tsSb.AppendLine("    }");
        tsSb.AppendLine();
        tsSb.AppendLine("    const text = await res.text().catch(() => '')");
        tsSb.AppendLine("    return { ok: false, error: `Mutation failed (${res.status}) ${method} ${url}\\n${text}` }");
        tsSb.AppendLine("}");
        tsSb.AppendLine();

        tsSb.AppendLine("export const mutationDefs = {");
        foreach (var m in mutations)
        {
            var invalidates = "[" + string.Join(", ", m.InvalidatesComponentTypeNames.Select(x => $"'{x}'")) + "]";
            tsSb.AppendLine($"    {m.MethodName}: {{ method: '{m.HttpMethod}', path: '{m.Path}', invalidates: {invalidates} }},");
        }
        if (mutations.Count == 0)
        {
            tsSb.AppendLine("    // No mutations annotated with .AsMutation<TReq, TRes>() yet.");
        }
        tsSb.AppendLine("} as const");
        tsSb.AppendLine();

        foreach (var m in mutations)
        {
            var fn = $"mutate{m.MethodName}";
            var routeParams = ExtractRouteParams(m.Path);

            var paramSig = routeParams.Count == 0
                ? ""
                : string.Join(", ", routeParams.Select(p => $"{SanitizeIdentifier(p)}: string"));

            if (paramSig.Length > 0) paramSig += ", ";

            tsSb.AppendLine($"export async function {fn}({paramSig}req: {m.RequestType.Name}): Promise<MutationResult<{m.ResponseType.Name}>> {{");

            if (routeParams.Count == 0)
            {
                tsSb.AppendLine($"    const url = '{m.Path}'");
            }
            else
            {
                // Replace each `{param}` with `encodeURIComponent(param)`.
                var urlExpr = $"'{m.Path}'";
                foreach (var p in routeParams)
                {
                    var id = SanitizeIdentifier(p);
                    urlExpr = urlExpr.Replace("{" + p + "}", $"' + encodeURIComponent(String({id})) + '");
                }
                tsSb.AppendLine($"    const url = {urlExpr}");
            }

            tsSb.AppendLine($"    const result = await jsonMutation<{m.RequestType.Name}, {m.ResponseType.Name}>(url, '{m.HttpMethod}', req)");
            tsSb.AppendLine("    if (result.ok) {");
            foreach (var inv in m.InvalidatesComponentTypeNames)
            {
                tsSb.AppendLine($"        invalidateComponent('{inv}' as ComponentKey)");
            }
            tsSb.AppendLine("    }");
            tsSb.AppendLine("    return result");
            tsSb.AppendLine("}");
            tsSb.AppendLine();
        }

        // Embed TypeScript in block comment (valid C#)
        var csSb = new StringBuilder();
        csSb.AppendLine("// <auto-generated/>");
        csSb.AppendLine("/*");
        csSb.AppendLine("SHALIMAR_TS: shalimar-mutations.g.ts");
        csSb.Append(tsSb);
        csSb.AppendLine("END_SHALIMAR_TS");
        csSb.AppendLine("*/");

        context.AddSource("ShalimarMutations.g.cs", SourceText.From(csSb.ToString(), Encoding.UTF8));
    }

    private static void GenerateTypeScriptTypesEmbedded(
        SourceProductionContext context,
        Compilation compilation,
        List<ComponentInfo> components,
        List<MutationInfo> mutations)
    {
        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine();

        var emitter = new TypeScriptEmitter(compilation);

        // Collect all reachable types from component props.
        var roots = new List<ITypeSymbol>();
        foreach (var c in components)
        {
            roots.AddRange(c.Properties.Select(p => p.Type));
            // Also include the props type itself (so it always exists in TS output).
            // We can only resolve it by name; the props type is in the current compilation.
            var props = compilation.GetTypeByMetadataName($"{c.Namespace}.{c.TypeName}");
            if (props != null) roots.Add(props);
        }

        // Mutations expand the public TS surface too: include request/response types.
        foreach (var m in mutations)
        {
            roots.Add(m.RequestType);
            roots.Add(m.ResponseType);
        }

        var orderedTypes = emitter.CollectTypes(roots);

        foreach (var t in orderedTypes)
        {
            emitter.EmitType(tsSb, t);
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
        tsSb.AppendLine("export const routes = rootRoute('Features/App/root.tsx', [");

        // NOTE: We generate TanStack virtual file route configs from ASP.NET route paths.
        // This is intentionally path-driven so we can later generate nested route trees.
        // File mapping convention:
        //   "/"                      -> Features/Home/route.tsx
        //   "/tasks"                 -> Features/Tasks/route.tsx
        //   "/tasks/board"           -> Features/Tasks/Board/route.tsx
        //   "/tasks/{taskId}"        -> Features/Tasks/$taskId/route.tsx
        //   "/accounts/{accountId}"  -> Features/Accounts/$accountId/route.tsx
        // Each segment becomes a folder name (PascalCase) except parameter segments (`{x}`) which become `$x`.

        var mappings = components
            .Select(c =>
            {
                var normalized = c.Path == "/" ? "/" : "/" + c.Path.Trim('/');
                var file = !string.IsNullOrWhiteSpace(c.TsxFile) ? c.TsxFile! : MapPathToRouteFile(normalized);
                return (Path: normalized, FilePath: file);
            })
            .ToList();

        var routesTree = BuildRoutesTree(mappings);
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

    private static RouteNode BuildRoutesTree(List<(string Path, string FilePath)> mappings)
    {
        var root = new RouteNode("");

        // Ensure deterministic ordering.
        foreach (var m in mappings
                     .GroupBy(x => x.Path, StringComparer.Ordinal)
                     .Select(g => g.First())
                     .OrderBy(x => x.Path, StringComparer.Ordinal))
        {
            var normalized = m.Path == "/" ? "/" : "/" + m.Path.Trim('/'); // normalize
            if (normalized == "/")
            {
                root.FilePath = m.FilePath;
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

            current.FilePath = m.FilePath;
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
            {
                var inner = p.Substring(1, p.Length - 2);
                var name = inner.Split(':')[0].TrimEnd('?');
                return "$" + name;
            }
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
            ? "$" + node.Segment.Substring(1, node.Segment.Length - 2).Split(':')[0].TrimEnd('?')
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

    private static void GenerateTypeScriptFacadesEmbedded(SourceProductionContext context)
    {
        static void EmitFacade(SourceProductionContext ctx, string hintName, string fileName, string ts)
        {
            var cs = new StringBuilder();
            cs.AppendLine("// <auto-generated/>");
            cs.AppendLine("/*");
            cs.AppendLine($"SHALIMAR_TS: {fileName}");
            cs.AppendLine(ts);
            cs.AppendLine("END_SHALIMAR_TS");
            cs.AppendLine("*/");
            ctx.AddSource(hintName, SourceText.From(cs.ToString(), Encoding.UTF8));
        }

        // Stable import paths for app code (avoid importing *.g.ts directly everywhere).
        EmitFacade(context, "ShalimarFacade.Types.g.cs", "types.ts", "export * from './shalimar-types.g'");
        EmitFacade(context, "ShalimarFacade.Paths.g.cs", "paths.ts", "export * from './shalimar-paths.g'");
        EmitFacade(context, "ShalimarFacade.Schemas.g.cs", "schemas.ts", "export * from './shalimar-zod-schemas.g'");
        EmitFacade(context, "ShalimarFacade.Defaults.g.cs", "defaults.ts", "export * from './shalimar-defaults.g'");
        EmitFacade(context, "ShalimarFacade.UseMutations.g.cs", "useMutations.ts", "export * from './shalimar-use-mutations.g'");
        EmitFacade(
            context,
            "ShalimarFacade.Store.g.cs",
            "store.ts",
            """
            export * from './shalimar-invalidations.g'
            export * from './shalimar-mutations.g'
            """);
    }

    private static void GenerateTypeScriptUseMutationsEmbedded(
        SourceProductionContext context,
        Compilation compilation,
        List<MutationInfo> mutations)
    {
        if (mutations.Count == 0) return;

        var requests = mutations.Select(m => m.RequestType).OfType<INamedTypeSymbol>().Distinct(SymbolEqualityComparer.Default).ToList();
        var responses = mutations.Select(m => m.ResponseType).OfType<INamedTypeSymbol>().Distinct(SymbolEqualityComparer.Default).ToList();

        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine("import type { z } from 'zod'");
        tsSb.AppendLine("import type { ValidationErrors, MutationResult } from './shalimar-mutations.g'");
        tsSb.AppendLine("import {");
        foreach (var m in mutations.OrderBy(m => m.MethodName, StringComparer.Ordinal))
            tsSb.AppendLine($"    mutate{m.MethodName},");
        tsSb.AppendLine("} from './shalimar-mutations.g'");
        tsSb.AppendLine("import {");
        foreach (var r in requests.Where(r => r is not null).OrderBy(r => r!.Name, StringComparer.Ordinal))
            tsSb.AppendLine($"    {r!.Name}Defaults,");
        tsSb.AppendLine("} from './shalimar-defaults.g'");
        tsSb.AppendLine("import {");
        foreach (var r in requests.Where(r => r is not null).OrderBy(r => r!.Name, StringComparer.Ordinal))
            tsSb.AppendLine($"    {r!.Name}Schema,");
        tsSb.AppendLine("} from './shalimar-zod-schemas.g'");
        tsSb.AppendLine("import type {");
        foreach (var t in requests.Concat(responses).Where(t => t is not null).OrderBy(t => t!.Name, StringComparer.Ordinal))
            tsSb.AppendLine($"    {t!.Name},");
        tsSb.AppendLine("} from './shalimar-types.g'");
        tsSb.AppendLine();

        tsSb.AppendLine("export type MutationSpec<TReq, TRes> = {");
        tsSb.AppendLine("    defaults: TReq");
        tsSb.AppendLine("    schema: z.ZodTypeAny");
        tsSb.AppendLine("    mutate: (req: TReq) => Promise<MutationResult<TRes>>");
        tsSb.AppendLine("    // Optional: server keys for mapping nested validation (PascalCase + [index])");
        tsSb.AppendLine("    serverKey: (path: Array<string | number>) => string");
        tsSb.AppendLine("    mapClientIssues?: (issues: Array<{ path: Array<string | number>; message: string }>) => ValidationErrors");
        tsSb.AppendLine("}");
        tsSb.AppendLine();

        tsSb.AppendLine("function toPascalCase(s: string) {");
        tsSb.AppendLine("    if (!s) return s");
        tsSb.AppendLine("    return s.length === 1 ? s.toUpperCase() : s[0].toUpperCase() + s.slice(1)");
        tsSb.AppendLine("}");
        tsSb.AppendLine();
        tsSb.AppendLine("function defaultServerKey(path: Array<string | number>): string {");
        tsSb.AppendLine("    if (path.length === 0) return 'Request'");
        tsSb.AppendLine("    const [first, ...rest] = path");
        tsSb.AppendLine("    let key = typeof first === 'string' ? toPascalCase(first) : String(first)");
        tsSb.AppendLine("    for (const seg of rest) {");
        tsSb.AppendLine("        if (typeof seg === 'number') key += `[${seg}]`");
        tsSb.AppendLine("        else key += `.${seg}`");
        tsSb.AppendLine("    }");
        tsSb.AppendLine("    return key");
        tsSb.AppendLine("}");
        tsSb.AppendLine();
        tsSb.AppendLine("function mapIssues(issues: Array<{ path: Array<string | number>; message: string }>): ValidationErrors {");
        tsSb.AppendLine("    const out: ValidationErrors = {}");
        tsSb.AppendLine("    for (const i of issues) {");
        tsSb.AppendLine("        const k = defaultServerKey(i.path)");
        tsSb.AppendLine("        out[k] ??= []");
        tsSb.AppendLine("        out[k].push(i.message)");
        tsSb.AppendLine("    }");
        tsSb.AppendLine("    return out");
        tsSb.AppendLine("}");
        tsSb.AppendLine();

        tsSb.AppendLine("export const mutationSpecs = {");
        foreach (var m in mutations.OrderBy(m => m.MethodName, StringComparer.Ordinal))
        {
            var req = m.RequestType.Name;
            var res = m.ResponseType.Name;
            // Key is the logical mutation name, value points at the generated mutate function.
            tsSb.AppendLine($"    {m.MethodName}: {{ defaults: {req}Defaults, schema: {req}Schema, mutate: mutate{m.MethodName}, serverKey: defaultServerKey, mapClientIssues: mapIssues }} satisfies MutationSpec<{req}, {res}>,");
        }
        tsSb.AppendLine("} as const");
        tsSb.AppendLine();

        tsSb.AppendLine("export function useMutations() {");
        tsSb.AppendLine("    return mutationSpecs");
        tsSb.AppendLine("}");
        tsSb.AppendLine();

        var csSb = new StringBuilder();
        csSb.AppendLine("// <auto-generated/>");
        csSb.AppendLine("/*");
        csSb.AppendLine("SHALIMAR_TS: shalimar-use-mutations.g.ts");
        csSb.Append(tsSb);
        csSb.AppendLine("END_SHALIMAR_TS");
        csSb.AppendLine("*/");

        context.AddSource("ShalimarUseMutations.g.cs", SourceText.From(csSb.ToString(), Encoding.UTF8));
    }

    private sealed class ValidatorRules
    {
        public Dictionary<string, PropertyRules> Properties { get; } = new Dictionary<string, PropertyRules>(StringComparer.Ordinal);
    }

    private sealed class PropertyRules
    {
        public bool NotEmpty { get; set; }
        public bool NotNull { get; set; }
        public int? MaxLength { get; set; }
        public int? MinLength { get; set; }
        public long? MinNumber { get; set; }
        public List<string>? AllowedValues { get; set; }
        public bool AppliesToEachElement { get; set; }
    }

    private static void GenerateTypeScriptZodSchemasEmbedded(
        SourceProductionContext context,
        Compilation compilation,
        List<MutationInfo> mutations)
    {
        IEqualityComparer<INamedTypeSymbol> namedSymbolComparer = SymbolEqualityComparer.Default;
        var requestTypes = mutations
            .Select(m => m.RequestType)
            .OfType<INamedTypeSymbol>()
            .Distinct(namedSymbolComparer)
            .ToImmutableArray();

        if (requestTypes.Length == 0) return;

        var validators = CollectFluentValidationRules(compilation);
        var requestTypeList = requestTypes.Where(t => t is not null).ToList();

        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine("import { z } from 'zod'");
        tsSb.AppendLine("import type {");
        foreach (var t in requestTypeList.OrderBy(t => t!.Name, StringComparer.Ordinal))
            tsSb.AppendLine($"    {t!.Name},");
        tsSb.AppendLine("} from './shalimar-types.g'");
        tsSb.AppendLine();

        foreach (var type in requestTypeList.OrderBy(t => t!.Name, StringComparer.Ordinal))
        {
            ValidatorRules rules;
            if (!validators.TryGetValue(type!, out rules) || rules is null)
                rules = new ValidatorRules();
            tsSb.AppendLine($"export const {type!.Name}Schema = {EmitZodSchemaForType(type!, rules)}");
            tsSb.AppendLine();
        }

        tsSb.AppendLine("export const schemas = {");
        foreach (var type in requestTypeList.OrderBy(t => t!.Name, StringComparer.Ordinal))
            tsSb.AppendLine($"    {type!.Name}: {type!.Name}Schema,");
        tsSb.AppendLine("} as const");
        tsSb.AppendLine();

        var csSb = new StringBuilder();
        csSb.AppendLine("// <auto-generated/>");
        csSb.AppendLine("/*");
        csSb.AppendLine("SHALIMAR_TS: shalimar-zod-schemas.g.ts");
        csSb.Append(tsSb);
        csSb.AppendLine("END_SHALIMAR_TS");
        csSb.AppendLine("*/");

        context.AddSource("ShalimarZodSchemas.g.cs", SourceText.From(csSb.ToString(), Encoding.UTF8));
    }

    private static void GenerateTypeScriptDefaultsEmbedded(
        SourceProductionContext context,
        Compilation compilation,
        List<MutationInfo> mutations)
    {
        IEqualityComparer<INamedTypeSymbol> namedSymbolComparer = SymbolEqualityComparer.Default;
        var requestTypes = mutations
            .Select(m => m.RequestType)
            .OfType<INamedTypeSymbol>()
            .Distinct(namedSymbolComparer)
            .ToImmutableArray();

        if (requestTypes.Length == 0) return;

        var validators = CollectFluentValidationRules(compilation);
        var requestTypeList = requestTypes.Where(t => t is not null).ToList();

        var tsSb = new StringBuilder();
        tsSb.AppendLine("// Generated by Shalimar - DO NOT EDIT");
        tsSb.AppendLine("import type {");
        foreach (var t in requestTypeList.OrderBy(t => t!.Name, StringComparer.Ordinal))
            tsSb.AppendLine($"    {t!.Name},");
        tsSb.AppendLine("} from './shalimar-types.g'");
        tsSb.AppendLine();

        foreach (var type in requestTypeList.OrderBy(t => t!.Name, StringComparer.Ordinal))
        {
            ValidatorRules rules;
            if (!validators.TryGetValue(type!, out rules) || rules is null)
                rules = new ValidatorRules();
            tsSb.AppendLine($"export const {type!.Name}Defaults: {type!.Name} = {EmitDefaultsForType(type!, rules)}");
            tsSb.AppendLine();
        }

        tsSb.AppendLine("export const defaults = {");
        foreach (var type in requestTypeList.OrderBy(t => t!.Name, StringComparer.Ordinal))
            tsSb.AppendLine($"    {type!.Name}: {type!.Name}Defaults,");
        tsSb.AppendLine("} as const");
        tsSb.AppendLine();

        var csSb = new StringBuilder();
        csSb.AppendLine("// <auto-generated/>");
        csSb.AppendLine("/*");
        csSb.AppendLine("SHALIMAR_TS: shalimar-defaults.g.ts");
        csSb.Append(tsSb);
        csSb.AppendLine("END_SHALIMAR_TS");
        csSb.AppendLine("*/");

        context.AddSource("ShalimarDefaults.g.cs", SourceText.From(csSb.ToString(), Encoding.UTF8));
    }

    private static Dictionary<INamedTypeSymbol, ValidatorRules> CollectFluentValidationRules(Compilation compilation)
    {
        var abstractValidator = compilation.GetTypeByMetadataName("FluentValidation.AbstractValidator`1");
        if (abstractValidator is null)
            return new Dictionary<INamedTypeSymbol, ValidatorRules>(SymbolEqualityComparer.Default);

        var validators = new Dictionary<INamedTypeSymbol, ValidatorRules>(SymbolEqualityComparer.Default);

        foreach (var type in GetAllNamedTypes(compilation.Assembly.GlobalNamespace))
        {
            if (!TryGetAbstractValidatorTarget(type, abstractValidator, out var target))
                continue;

            if (target is null) continue;
            var rules = ParseValidatorSyntax(type);
            validators[target] = rules;
        }

        return validators;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
        {
            yield return t;
            foreach (var nested in GetAllNestedTypes(t))
                yield return nested;
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var t in GetAllNamedTypes(child))
                yield return t;
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNestedTypes(INamedTypeSymbol type)
    {
        foreach (var t in type.GetTypeMembers())
        {
            yield return t;
            foreach (var nested in GetAllNestedTypes(t))
                yield return nested;
        }
    }

    private static bool TryGetAbstractValidatorTarget(INamedTypeSymbol candidate, INamedTypeSymbol abstractValidator, out INamedTypeSymbol? target)
    {
        target = null;
        for (var b = candidate.BaseType; b is not null; b = b.BaseType)
        {
            if (!SymbolEqualityComparer.Default.Equals(b.OriginalDefinition, abstractValidator))
                continue;
            target = (INamedTypeSymbol)b.TypeArguments[0];
            return true;
        }
        return false;
    }

    private static ValidatorRules ParseValidatorSyntax(INamedTypeSymbol validatorType)
    {
        var rules = new ValidatorRules();
        var syntaxRef = validatorType.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is null) return rules;

        var classDecl = syntaxRef.GetSyntax() as ClassDeclarationSyntax;
        if (classDecl is null) return rules;

        var boolMethods = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.BoolKeyword))
            .ToDictionary(m => m.Identifier.ValueText, m => m, StringComparer.Ordinal);

        foreach (var ctor in classDecl.Members.OfType<ConstructorDeclarationSyntax>())
        {
            if (ctor.Body is null) continue;
            foreach (var stmt in ctor.Body.Statements.OfType<ExpressionStatementSyntax>())
            {
                if (stmt.Expression is not InvocationExpressionSyntax inv) continue;
                var chain = UnwindFluentChain(inv);
                if (chain.Count == 0) continue;

                var root = chain[0];
                if (root.MethodName != "RuleFor" && root.MethodName != "RuleForEach") continue;
                if (root.Args.Count == 0) continue;

                var prop = TryExtractPropertyNameFromLambda(root.Args[0].Expression);
                if (prop is null) continue;

                var propKey = ToCamelCase(prop);
                PropertyRules pr;
                if (!rules.Properties.TryGetValue(propKey, out pr))
                {
                    pr = new PropertyRules();
                    rules.Properties[propKey] = pr;
                }

                pr.AppliesToEachElement = root.MethodName == "RuleForEach";

                foreach (var step in chain.Skip(1))
                {
                    switch (step.MethodName)
                    {
                        case "NotEmpty":
                            pr.NotEmpty = true;
                            break;
                        case "NotNull":
                            pr.NotNull = true;
                            break;
                        case "MaximumLength":
                            if (step.Args.Count == 1 && step.Args[0].Expression is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NumericLiteralExpression))
                                pr.MaxLength = Convert.ToInt32(lit.Token.Value);
                            break;
                        case "MinimumLength":
                            if (step.Args.Count == 1 && step.Args[0].Expression is LiteralExpressionSyntax litMin && litMin.IsKind(SyntaxKind.NumericLiteralExpression))
                                pr.MinLength = Convert.ToInt32(litMin.Token.Value);
                            break;
                        case "GreaterThanOrEqualTo":
                            if (step.Args.Count == 1 && step.Args[0].Expression is LiteralExpressionSyntax litGte && litGte.IsKind(SyntaxKind.NumericLiteralExpression))
                                pr.MinNumber = Convert.ToInt64(litGte.Token.Value);
                            break;
                        case "Must":
                            if (step.Args.Count == 1 && step.Args[0].Expression is IdentifierNameSyntax ident)
                            {
                                MethodDeclarationSyntax m;
                                if (boolMethods.TryGetValue(ident.Identifier.ValueText, out m))
                                {
                                    var allowed = ExtractStringLiterals(m).Distinct(StringComparer.Ordinal).ToList();
                                    if (allowed.Count > 0)
                                        pr.AllowedValues = allowed;
                                }
                            }
                            break;
                    }
                }
            }
        }

        return rules;
    }

    private sealed class FluentCall
    {
        public string MethodName { get; }
        public SeparatedSyntaxList<ArgumentSyntax> Args { get; }
        public FluentCall(string methodName, SeparatedSyntaxList<ArgumentSyntax> args)
        {
            MethodName = methodName;
            Args = args;
        }
    }

    private static List<FluentCall> UnwindFluentChain(InvocationExpressionSyntax invocation)
    {
        var output = new List<FluentCall>();
        InvocationExpressionSyntax current = invocation;

        while (true)
        {
            string name = null!;
            if (current.Expression is MemberAccessExpressionSyntax ma)
                name = ma.Name.Identifier.ValueText;
            else if (current.Expression is IdentifierNameSyntax id)
                name = id.Identifier.ValueText;
            else
                break;

            output.Add(new FluentCall(name, current.ArgumentList.Arguments));

            if (current.Expression is MemberAccessExpressionSyntax ma2 && ma2.Expression is InvocationExpressionSyntax nextInv)
                current = nextInv;
            else
                break;
        }

        output.Reverse();
        return output;
    }

    private static string? TryExtractPropertyNameFromLambda(ExpressionSyntax expr)
    {
        if (expr is SimpleLambdaExpressionSyntax simple && simple.ExpressionBody is MemberAccessExpressionSyntax ma)
            return ma.Name.Identifier.ValueText;
        if (expr is ParenthesizedLambdaExpressionSyntax paren && paren.ExpressionBody is MemberAccessExpressionSyntax ma2)
            return ma2.Name.Identifier.ValueText;
        return null;
    }

    private static IEnumerable<string> ExtractStringLiterals(MethodDeclarationSyntax method)
    {
        if (method.Body is null) yield break;
        foreach (var lit in method.Body.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (!lit.IsKind(SyntaxKind.StringLiteralExpression)) continue;
            var s = lit.Token.ValueText;
            if (!string.IsNullOrWhiteSpace(s))
                yield return s;
        }
    }

    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length == 1) return s.ToLowerInvariant();
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    private static string EmitZodSchemaForType(INamedTypeSymbol type, ValidatorRules rules)
    {
        var props = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("z.object({");
        foreach (var p in props)
        {
            var name = ToCamelCase(p.Name);
            PropertyRules pr;
            if (!rules.Properties.TryGetValue(name, out pr) || pr is null)
                pr = new PropertyRules();
            sb.Append("    ");
            sb.Append(name);
            sb.Append(": ");
            sb.Append(EmitZodForProperty(p, pr));
            sb.AppendLine(",");
        }
        sb.Append("})");
        return sb.ToString();
    }

    private static string EmitDefaultsForType(INamedTypeSymbol type, ValidatorRules rules)
    {
        var props = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("{");
        foreach (var p in props)
        {
            var name = ToCamelCase(p.Name);
            PropertyRules pr;
            if (!rules.Properties.TryGetValue(name, out pr) || pr is null)
                pr = new PropertyRules();
            sb.Append("    ");
            sb.Append(name);
            sb.Append(": ");
            sb.Append(EmitDefaultForProperty(p, pr));
            sb.AppendLine(",");
        }
        sb.Append("}");
        return sb.ToString();
    }

    private static string EmitZodForProperty(IPropertySymbol prop, PropertyRules rules)
    {
        var t = prop.Type;
        var isNullable = IsNullable(prop);

        string schema;
        if (IsString(t))
        {
            if (rules.AllowedValues is not null && rules.AllowedValues.Count > 0)
            {
                schema = $"z.enum([{string.Join(", ", rules.AllowedValues.Select(v => $"'{v}'"))}])";
            }
            else
            {
                schema = "z.string()";
            }

            if (rules.NotEmpty)
                schema += ".min(1)";
            if (rules.MinLength is not null)
                schema += $".min({rules.MinLength.Value})";
            if (rules.MaxLength is not null)
                schema += $".max({rules.MaxLength.Value})";
        }
        else if (IsNumeric(t))
        {
            schema = "z.number()";
            if (rules.MinNumber is not null)
                schema += $".min({rules.MinNumber.Value})";
        }
        else if (IsBool(t))
        {
            schema = "z.boolean()";
        }
        else if (IsStringArrayLike(t))
        {
            var element = "z.string()";
            if (rules.AppliesToEachElement && rules.NotEmpty)
                element += ".min(1)";
            schema = $"z.array({element})";
            if (!rules.AppliesToEachElement && rules.NotEmpty)
                schema += ".min(1)";
        }
        else
        {
            schema = "z.any()";
        }

        if (isNullable && !rules.NotNull)
            schema += ".nullable()";

        return schema;
    }

    private static string EmitDefaultForProperty(IPropertySymbol prop, PropertyRules rules)
    {
        var isNullable = IsNullable(prop);
        if (isNullable && !rules.NotNull && !rules.NotEmpty)
            return "null";

        var t = prop.Type;
        if (IsString(t)) return "''";
        if (IsNumeric(t)) return "0";
        if (IsBool(t)) return "false";
        if (IsStringArrayLike(t)) return "[]";
        return "null as any";
    }

    private static bool IsNullable(IPropertySymbol prop)
    {
        if (prop.NullableAnnotation == NullableAnnotation.Annotated)
            return true;

        var named = prop.Type as INamedTypeSymbol;
        if (named is not null && named.IsGenericType && named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            return true;

        return false;
    }

    private static bool IsString(ITypeSymbol t) => t.SpecialType == SpecialType.System_String;
    private static bool IsBool(ITypeSymbol t) => t.SpecialType == SpecialType.System_Boolean;
    private static bool IsNumeric(ITypeSymbol t) =>
        t.SpecialType == SpecialType.System_Int32 ||
        t.SpecialType == SpecialType.System_Int64 ||
        t.SpecialType == SpecialType.System_Double ||
        t.SpecialType == SpecialType.System_Single ||
        t.SpecialType == SpecialType.System_Decimal;

    private static bool IsStringArrayLike(ITypeSymbol t)
    {
        if (t is IArrayTypeSymbol arr)
            return arr.ElementType.SpecialType == SpecialType.System_String;

        var named = t as INamedTypeSymbol;
        if (named is null || !named.IsGenericType || named.TypeArguments.Length != 1)
            return false;

        var arg = named.TypeArguments[0];
        if (arg.SpecialType != SpecialType.System_String)
            return false;

        var name = named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return name.IndexOf("System.Collections.Generic.IReadOnlyList", StringComparison.Ordinal) >= 0 ||
               name.IndexOf("System.Collections.Generic.List", StringComparison.Ordinal) >= 0 ||
               name.IndexOf("System.Collections.Generic.IEnumerable", StringComparison.Ordinal) >= 0;
    }

    // Using classes instead of records for netstandard2.0 compatibility
    private sealed class ComponentInfo
    {
        public string TypeName { get; }
        public string Namespace { get; }
        public string Path { get; }
        public ImmutableArray<PropertyInfo> Properties { get; }
        public string? TsxFile { get; }

        public ComponentInfo(string typeName, string ns, string path, ImmutableArray<PropertyInfo> properties, string? tsxFile)
        {
            TypeName = typeName;
            Namespace = ns;
            Path = path;
            Properties = properties;
            TsxFile = tsxFile;
        }
    }

    private sealed class PropertyInfo
    {
        public string Name { get; }
        public ITypeSymbol Type { get; }

        public PropertyInfo(string name, ITypeSymbol type)
        {
            Name = name;
            Type = type;
        }
    }

    private sealed class TypeScriptEmitter
    {
        private readonly Compilation _compilation;
        private readonly INamedTypeSymbol? _dateTime;
        private readonly INamedTypeSymbol? _dateTimeOffset;
        private readonly INamedTypeSymbol? _guid;

        public TypeScriptEmitter(Compilation compilation)
        {
            _compilation = compilation;
            _dateTime = compilation.GetTypeByMetadataName("System.DateTime");
            _dateTimeOffset = compilation.GetTypeByMetadataName("System.DateTimeOffset");
            _guid = compilation.GetTypeByMetadataName("System.Guid");
        }

        public List<INamedTypeSymbol> CollectTypes(IEnumerable<ITypeSymbol> roots)
        {
            var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var output = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
            var queue = new Queue<ITypeSymbol>();
            foreach (var r in roots)
            {
                if (r != null) queue.Enqueue(r);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null) continue;
                if (!visited.Add(current)) continue;

                // Unwrap nullable.
                if (current is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && named.TypeArguments.Length == 1)
                {
                    queue.Enqueue(named.TypeArguments[0]);
                    continue;
                }

                // For generic user objects, enqueue the generic definition for emission and also traverse type arguments.
                if (current is INamedTypeSymbol gen && gen.IsGenericType && gen.TypeArguments.Length > 0)
                {
                    foreach (var ta in gen.TypeArguments)
                        queue.Enqueue(ta);

                    // Prefer emitting the open generic definition (Deferred<T>) instead of a constructed type (Deferred<CrmInsights>).
                    var def = gen.OriginalDefinition;
                    if (!SymbolEqualityComparer.Default.Equals(def, current))
                        queue.Enqueue(def);
                }

                // Arrays / collections / dictionaries.
                var element = TryGetEnumerableElement(current);
                if (element != null)
                {
                    queue.Enqueue(element);
                    continue;
                }

                var dictValue = TryGetDictionaryValue(current);
                if (dictValue != null)
                {
                    queue.Enqueue(dictValue);
                    continue;
                }

                // Only generate shapes for user types (records/classes) and enums.
                if (IsPrimitive(current) || IsWellKnownScalar(current))
                    continue;

                if (current.TypeKind == TypeKind.Enum)
                {
                    if (current is INamedTypeSymbol e)
                        output[e.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)] = e;
                    continue;
                }

                if (current is INamedTypeSymbol obj && ShouldEmitObject(obj))
                {
                    // Emit open generic definitions (if present) for stable TS output.
                    var emit = obj.IsGenericType ? obj.OriginalDefinition : obj;
                    output[emit.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)] = emit;
                    foreach (var p in GetPublicProperties(obj))
                        queue.Enqueue(p.Type);
                }
            }

            return output
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => kvp.Value)
                .ToList();
        }

        public void EmitType(StringBuilder sb, INamedTypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                var members = type.GetMembers().OfType<IFieldSymbol>()
                    .Where(f => f.HasConstantValue && f.Name != "value__")
                    .Select(f => $"'{f.Name}'");
                var union = string.Join(" | ", members);
                if (string.IsNullOrWhiteSpace(union)) union = "string";
                sb.AppendLine($"export type {type.Name} = {union}");
                return;
            }

            var typeParams = "";
            if (type.IsGenericType && type.TypeArguments.Length > 0)
            {
                // Use definition type parameters when possible (e.g., Deferred<T>).
                var def = type.OriginalDefinition;
                if (def.TypeParameters.Length > 0)
                {
                    typeParams = "<" + string.Join(", ", def.TypeParameters.Select(p => p.Name)) + ">";
                }
            }

            sb.AppendLine($"export interface {type.Name}{typeParams} {{");
            foreach (var prop in GetPublicProperties(type))
            {
                var camelName = char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1);
                sb.AppendLine($"    {camelName}: {ToTsType(prop.Type)};");
            }
            sb.AppendLine("}");
        }

        private IEnumerable<IPropertySymbol> GetPublicProperties(INamedTypeSymbol type) =>
            type.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic);

        private bool ShouldEmitObject(INamedTypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None) return false;
            if (type.ContainingNamespace?.ToDisplayString() == "System") return false;
            if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct) return false;
            return true;
        }

        private bool IsPrimitive(ITypeSymbol type) =>
            type.SpecialType is SpecialType.System_String or SpecialType.System_Boolean or
            SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single or
            SpecialType.System_Double or SpecialType.System_Decimal;

        private bool IsWellKnownScalar(ITypeSymbol type)
        {
            if (_dateTime != null && SymbolEqualityComparer.Default.Equals(type, _dateTime)) return true;
            if (_dateTimeOffset != null && SymbolEqualityComparer.Default.Equals(type, _dateTimeOffset)) return true;
            if (_guid != null && SymbolEqualityComparer.Default.Equals(type, _guid)) return true;
            return false;
        }

        private ITypeSymbol? TryGetEnumerableElement(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol arr) return arr.ElementType;

            if (type is INamedTypeSymbol named)
            {
                if (named.IsGenericType && named.TypeArguments.Length == 1)
                {
                    var def = named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (def == "global::System.Collections.Generic.IEnumerable<T>" ||
                        def == "global::System.Collections.Generic.IReadOnlyList<T>" ||
                        def == "global::System.Collections.Generic.IReadOnlyCollection<T>" ||
                        def == "global::System.Collections.Generic.List<T>" ||
                        def == "global::System.Collections.Generic.IList<T>" ||
                        def == "global::System.Collections.Generic.ICollection<T>")
                    {
                        return named.TypeArguments[0];
                    }
                }

                foreach (var i in named.AllInterfaces)
                {
                    if (!i.IsGenericType || i.TypeArguments.Length != 1) continue;
                    var def = i.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (def == "global::System.Collections.Generic.IEnumerable<T>")
                        return i.TypeArguments[0];
                }
            }

            return null;
        }

        private ITypeSymbol? TryGetDictionaryValue(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named)
            {
                if (named.IsGenericType && named.TypeArguments.Length == 2)
                {
                    var def = named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (def == "global::System.Collections.Generic.Dictionary<TKey, TValue>" ||
                        def == "global::System.Collections.Generic.IDictionary<TKey, TValue>" ||
                        def == "global::System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
                    {
                        // We only support string keys for now (server-truth safe default).
                        if (named.TypeArguments[0].SpecialType == SpecialType.System_String)
                            return named.TypeArguments[1];
                    }
                }

                foreach (var i in named.AllInterfaces)
                {
                    if (!i.IsGenericType || i.TypeArguments.Length != 2) continue;
                    var def = i.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (def == "global::System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>" &&
                        i.TypeArguments[0].SpecialType == SpecialType.System_String)
                        return i.TypeArguments[1];
                }
            }

            return null;
        }

        private string ToTsType(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol tp)
                return tp.Name;

            // Handle nullable reference types (C# 8+).
            if (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated)
            {
                // Strip annotation so we don't recurse forever.
                var nonNull = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                return $"{ToTsType(nonNull)} | null";
            }

            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && named.TypeArguments.Length == 1)
                return $"{ToTsType(named.TypeArguments[0])} | null";

            if (type.SpecialType == SpecialType.System_String) return "string";
            if (type.SpecialType == SpecialType.System_Boolean) return "boolean";
            if (type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 or
                SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal)
                return "number";

            if (IsWellKnownScalar(type)) return "string";

            var element = TryGetEnumerableElement(type);
            if (element != null) return $"{ToTsType(element)}[]";

            var dictValue = TryGetDictionaryValue(type);
            if (dictValue != null) return $"Record<string, {ToTsType(dictValue)}>";

            if (type.TypeKind == TypeKind.Enum) return type.Name;

            if (type is INamedTypeSymbol obj && ShouldEmitObject(obj))
            {
                if (obj.IsGenericType && obj.TypeArguments.Length > 0)
                {
                    var args = string.Join(", ", obj.TypeArguments.Select(ToTsType));
                    return $"{obj.Name}<{args}>";
                }
                return obj.Name;
            }

            // Safe default to avoid generated TS breaking builds.
            return "unknown";
        }
    }

    private sealed class DeferredInfo
    {
        public ITypeSymbol ResultType { get; }
        public string Path { get; }
        public string RefMethodName { get; }
        public string? ParentComponentTypeName { get; }
        public string? NodePath { get; }

        public DeferredInfo(ITypeSymbol resultType, string path, string refMethodName, string? parentComponentTypeName, string? nodePath)
        {
            ResultType = resultType;
            Path = path;
            RefMethodName = refMethodName;
            ParentComponentTypeName = parentComponentTypeName;
            NodePath = nodePath;
        }
    }

    private sealed class LazyInfo
    {
        public ITypeSymbol ResultType { get; }
        public string Path { get; }
        public string RefMethodName { get; }
        public string? ParentComponentTypeName { get; }
        public string? NodePath { get; }

        public LazyInfo(ITypeSymbol resultType, string path, string refMethodName, string? parentComponentTypeName, string? nodePath)
        {
            ResultType = resultType;
            Path = path;
            RefMethodName = refMethodName;
            ParentComponentTypeName = parentComponentTypeName;
            NodePath = nodePath;
        }
    }

    private sealed class StreamInfo
    {
        public ITypeSymbol EventType { get; }
        public string Path { get; }
        public string RefMethodName { get; }
        public string? ParentComponentTypeName { get; }
        public string? NodePath { get; }

        public StreamInfo(ITypeSymbol eventType, string path, string refMethodName, string? parentComponentTypeName, string? nodePath)
        {
            EventType = eventType;
            Path = path;
            RefMethodName = refMethodName;
            ParentComponentTypeName = parentComponentTypeName;
            NodePath = nodePath;
        }
    }

    private sealed class SseInfo
    {
        public ITypeSymbol EventType { get; }
        public string Path { get; }
        public string RefMethodName { get; }
        public string? ParentComponentTypeName { get; }
        public string? NodePath { get; }

        public SseInfo(ITypeSymbol eventType, string path, string refMethodName, string? parentComponentTypeName, string? nodePath)
        {
            EventType = eventType;
            Path = path;
            RefMethodName = refMethodName;
            ParentComponentTypeName = parentComponentTypeName;
            NodePath = nodePath;
        }
    }

    private sealed class MutationInfo
    {
        public ITypeSymbol RequestType { get; }
        public ITypeSymbol ResponseType { get; }
        public string Path { get; }
        public string MethodName { get; }
        public string HttpMethod { get; }
        public IReadOnlyList<string> InvalidatesComponentTypeNames { get; }

        public MutationInfo(ITypeSymbol requestType, ITypeSymbol responseType, string path, string methodName, string httpMethod, IReadOnlyList<string> invalidates)
        {
            RequestType = requestType;
            ResponseType = responseType;
            Path = path;
            MethodName = methodName;
            HttpMethod = httpMethod;
            InvalidatesComponentTypeNames = invalidates;
        }
    }
}
