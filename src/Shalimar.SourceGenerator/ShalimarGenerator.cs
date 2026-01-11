using Microsoft.CodeAnalysis;
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

        return new ComponentInfo(
            typeSymbol.Name,
            typeSymbol.ContainingNamespace?.ToDisplayString() ?? "",
            routePath ?? "/",
            GetProperties(typeSymbol));
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
        return new DeferredInfo(typeSymbol, routePath!, method, parent);
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
        return new LazyInfo(typeSymbol, routePath!, method, parent);
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
        return new StreamInfo(typeSymbol, routePath!, method, parent);
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
        return new SseInfo(typeSymbol, routePath!, method, parent);
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

        var sse = source.Left4.Left3.Sse
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

        GenerateTypeScriptInvalidationsEmbedded(context, compilation, deferred, lazy, stream, sse);
        GenerateTypeScriptMutationsEmbedded(context, compilation, mutations);

        // Generate TypeScript embedded in C# files (for MSBuild task to extract)
        GenerateTypeScriptTypesEmbedded(context, compilation, components);
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

    private static void GenerateTypeScriptTypesEmbedded(
        SourceProductionContext context,
        Compilation compilation,
        List<ComponentInfo> components)
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

        public DeferredInfo(ITypeSymbol resultType, string path, string refMethodName, string? parentComponentTypeName)
        {
            ResultType = resultType;
            Path = path;
            RefMethodName = refMethodName;
            ParentComponentTypeName = parentComponentTypeName;
        }
    }

    private sealed class LazyInfo
    {
        public ITypeSymbol ResultType { get; }
        public string Path { get; }
        public string RefMethodName { get; }
        public string? ParentComponentTypeName { get; }

        public LazyInfo(ITypeSymbol resultType, string path, string refMethodName, string? parentComponentTypeName)
        {
            ResultType = resultType;
            Path = path;
            RefMethodName = refMethodName;
            ParentComponentTypeName = parentComponentTypeName;
        }
    }

    private sealed class StreamInfo
    {
        public ITypeSymbol EventType { get; }
        public string Path { get; }
        public string RefMethodName { get; }
        public string? ParentComponentTypeName { get; }

        public StreamInfo(ITypeSymbol eventType, string path, string refMethodName, string? parentComponentTypeName)
        {
            EventType = eventType;
            Path = path;
            RefMethodName = refMethodName;
            ParentComponentTypeName = parentComponentTypeName;
        }
    }

    private sealed class SseInfo
    {
        public ITypeSymbol EventType { get; }
        public string Path { get; }
        public string RefMethodName { get; }
        public string? ParentComponentTypeName { get; }

        public SseInfo(ITypeSymbol eventType, string path, string refMethodName, string? parentComponentTypeName)
        {
            EventType = eventType;
            Path = path;
            RefMethodName = refMethodName;
            ParentComponentTypeName = parentComponentTypeName;
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
