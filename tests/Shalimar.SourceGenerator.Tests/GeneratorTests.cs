using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shalimar.SourceGenerator;
using Xunit;

namespace Shalimar.SourceGenerator.Tests;

public class GeneratorTests
{
    [Fact]
    public void Generator_CanBeInstantiated()
    {
        var generator = new ShalimarGenerator();
        Assert.NotNull(generator);
    }

    [Fact]
    public void Generator_Emits_DeferredRefs_And_Deferred_Generic_Type()
    {
        var source = """
using System;

namespace Shalimar
{
    public sealed record Deferred<T>(string Href);
    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder AsDeferred<T>(this RouteHandlerBuilder builder) => builder;
    }
}

public sealed class RouteHandlerBuilder
{
    public RouteHandlerBuilder WithName(string name) => this;
}

public sealed class WebApplication
{
    public RouteHandlerBuilder MapGet(string pattern, Delegate handler) => new();
}

namespace TestApp;

public static class App
{
    public static void Configure(WebApplication app)
    {
        app.MapGet("/", () => new DashboardProps(new Shalimar.Deferred<CrmInsights>("/crm/insights")))
            .AsComponent<DashboardProps>();

        app.MapGet("/crm/insights", () => new CrmInsights("hello"))
            .AsDeferred<CrmInsights>();
    }
}

public sealed record DashboardProps(Shalimar.Deferred<CrmInsights> Insights);

public sealed record CrmInsights(string Summary);
""";

        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestApp",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var generator = new ShalimarGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = outputCompilation.SyntaxTrees
            .Where(t => t != syntaxTree)
            .Select(t => new
            {
                FileName = t.FilePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "unknown",
                Text = t.ToString()
            })
            .OrderBy(x => x.FileName)
            .ToImmutableArray();

        // Deferred slice expectations:
        // - A C# helper should be generated so server code can reference a typed deferred handle without duplicating strings.
        // - TS types should preserve generics: Deferred<T> rather than erasing T.

        Assert.Contains(generated, g => g.Text.Contains("public static class DeferredRefs", StringComparison.Ordinal));
        var shalimarTypes = Assert.Single(generated, g => g.Text.Contains("SHALIMAR_TS: shalimar-types.g.ts", StringComparison.Ordinal)).Text;
        Assert.Contains("export interface Deferred<T>", shalimarTypes, StringComparison.Ordinal);
        Assert.Contains("insights: Deferred<CrmInsights>", shalimarTypes, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_Emits_LazyRefs_And_Lazy_Generic_Type()
    {
        var source = """
using System;

namespace Shalimar
{
    public sealed record Lazy<T>(string Href);
    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder AsLazy<T>(this RouteHandlerBuilder builder) => builder;
    }
}

public sealed class RouteHandlerBuilder
{
    public RouteHandlerBuilder WithName(string name) => this;
}

public sealed class WebApplication
{
    public RouteHandlerBuilder MapGet(string pattern, Delegate handler) => new();
}

namespace TestApp;

public static class App
{
    public static void Configure(WebApplication app)
    {
        app.MapGet("/", () => new DashboardProps(new Shalimar.Lazy<CrmForecast>("/crm/forecast")))
            .AsComponent<DashboardProps>();

        app.MapGet("/crm/forecast", () => new CrmForecast("ok"))
            .AsLazy<CrmForecast>();
    }
}

public sealed record DashboardProps(Shalimar.Lazy<CrmForecast> Forecast);

public sealed record CrmForecast(string Summary);
""";

        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestApp",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var generator = new ShalimarGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = outputCompilation.SyntaxTrees
            .Where(t => t != syntaxTree)
            .Select(t => new
            {
                FileName = t.FilePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "unknown",
                Text = t.ToString()
            })
            .OrderBy(x => x.FileName)
            .ToImmutableArray();

        Assert.Contains(generated, g => g.Text.Contains("public static class LazyRefs", StringComparison.Ordinal));
        var shalimarTypes = Assert.Single(generated, g => g.Text.Contains("SHALIMAR_TS: shalimar-types.g.ts", StringComparison.Ordinal)).Text;
        Assert.Contains("export interface Lazy<T>", shalimarTypes, StringComparison.Ordinal);
        Assert.Contains("forecast: Lazy<CrmForecast>", shalimarTypes, StringComparison.Ordinal);
    }
}
