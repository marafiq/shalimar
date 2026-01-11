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

    [Fact]
    public void Generator_Emits_StreamRefs_And_Stream_Generic_Type()
    {
        var source = """
using System;

namespace Shalimar
{
    public sealed record Stream<T>(string Href);
    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder AsStream<T>(this RouteHandlerBuilder builder) => builder;
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
        app.MapGet("/", () => new DashboardProps(new Shalimar.Stream<ActivityEvent>("/crm/activity/stream")))
            .AsComponent<DashboardProps>();

        app.MapGet("/crm/activity/stream", () => new ActivityEvent("tick"))
            .AsStream<ActivityEvent>();
    }
}

public sealed record DashboardProps(Shalimar.Stream<ActivityEvent> ActivityStream);

public sealed record ActivityEvent(string Kind);
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

        Assert.Contains(generated, g => g.Text.Contains("public static class StreamRefs", StringComparison.Ordinal));
        var shalimarTypes = Assert.Single(generated, g => g.Text.Contains("SHALIMAR_TS: shalimar-types.g.ts", StringComparison.Ordinal)).Text;
        Assert.Contains("export interface Stream<T>", shalimarTypes, StringComparison.Ordinal);
        Assert.Contains("activityStream: Stream<ActivityEvent>", shalimarTypes, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_Emits_Typed_Route_Methods_With_Constraints()
    {
        var source = """
using System;

namespace Shalimar
{
    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) => builder;
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
        app.MapGet("/accounts/{accountId:guid}", () => new AccountProps("Account", Guid.Empty))
            .AsComponent<AccountProps>();

        app.MapGet("/tasks/{taskId:int}", () => new TaskProps("Task", 123))
            .AsComponent<TaskProps>();
    }
}

public sealed record AccountProps(string Message, Guid AccountId);
public sealed record TaskProps(string Message, int TaskId);
""";

        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
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
            .Select(t => t.ToString())
            .ToImmutableArray();

        var routes = Assert.Single(generated, t => t.Contains("namespace Shalimar.Generated;", StringComparison.Ordinal) && t.Contains("class Routes", StringComparison.Ordinal));

        Assert.Contains("public static string AccountProps => \"/accounts/{accountId:guid}\";", routes, StringComparison.Ordinal);
        Assert.Contains("public static string TaskProps => \"/tasks/{taskId:int}\";", routes, StringComparison.Ordinal);

        // Typed helpers (constraint-driven parameter types).
        Assert.Contains("AccountPropsPath(global::System.Guid", routes, StringComparison.Ordinal);
        Assert.Contains("TaskPropsPath(global::System.Int32", routes, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_Emits_Behaviors_Class_For_Component_Scoped_Modes()
    {
        var source = """
using System;

namespace Shalimar
{
    public sealed record Deferred<T>(string Href);
    public sealed record Component<TProps>(TProps Props);
    public sealed record ShalimarBehaviors(System.Collections.Generic.IReadOnlyList<string> DeferredHrefs, System.Collections.Generic.IReadOnlyList<string> LazyHrefs, System.Collections.Generic.IReadOnlyList<string> StreamedHrefs, System.Collections.Generic.IReadOnlyList<string> SseHrefs);
    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder ForComponent<TProps>(this RouteHandlerBuilder builder) => builder;
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
        app.MapGet("/", () => new DashboardProps(new Shalimar.Component<AgentPanelProps>(new AgentPanelProps(new Shalimar.Deferred<CrmInsights>("/crm/insights")))))
            .AsComponent<DashboardProps>();

        app.MapGet("/crm/insights", () => new CrmInsights("ok"))
            .ForComponent<AgentPanelProps>()
            .AsDeferred<CrmInsights>();
    }
}

public sealed record CrmInsights(string Summary);
public sealed record AgentPanelProps(Shalimar.Deferred<CrmInsights> Insights);
public sealed record DashboardProps(Shalimar.Component<AgentPanelProps> AgentPanel);
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
            .Select(t => t.ToString())
            .ToImmutableArray();

        var behaviors = Assert.Single(generated, t => t.Contains("public static class Behaviors", StringComparison.Ordinal));
        Assert.Contains("public static class AgentPanelProps", behaviors, StringComparison.Ordinal);
        Assert.Contains("PrefetchDeferred", behaviors, StringComparison.Ordinal);
        Assert.Contains("\"/crm/insights\"", behaviors, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_Emits_Zod_Schemas_And_Defaults_From_FluentValidation()
    {
        var source = """
using System;

namespace FluentValidation
{
    public interface IRuleBuilder<T, TProperty>
    {
        IRuleBuilder<T, TProperty> NotEmpty();
        IRuleBuilder<T, TProperty> NotNull();
        IRuleBuilder<T, TProperty> MaximumLength(int max);
        IRuleBuilder<T, TProperty> GreaterThanOrEqualTo(int min);
        IRuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate);
        IRuleBuilder<T, TProperty> When(Func<T, bool> predicate);
    }

    public abstract class AbstractValidator<T>
    {
        protected IRuleBuilder<T, TProperty> RuleFor<TProperty>(Func<T, TProperty> expr) => default!;
        protected IRuleBuilder<T, TProperty> RuleForEach<TProperty>(Func<T, TProperty> expr) => default!;
    }
}

namespace Shalimar
{
    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsMutation<TReq, TRes>(this RouteHandlerBuilder builder) => builder;
    }
}

public sealed class RouteHandlerBuilder
{
    public RouteHandlerBuilder WithName(string name) => this;
}

public sealed class WebApplication
{
    public RouteHandlerBuilder MapPost(string pattern, Delegate handler) => new();
}

namespace TestApp;

public static class App
{
    public static void Configure(WebApplication app)
    {
        app.MapPost("/crm/tasks", (CreateTaskRequest req) => new TaskDto(req.Title))
            .AsMutation<CreateTaskRequest, TaskDto>();
    }
}

public sealed record CreateTaskRequest(string Title, string? Priority, int? EstimateMinutes);
public sealed record TaskDto(string Title);

public sealed class CreateTaskRequestValidator : FluentValidation.AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EstimateMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Priority).Must(BePriority).When(x => x.Priority != null);
    }

    private static bool BePriority(string? p) => p is null || p is "low" or "medium" or "high";
}
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
            .Select(t => t.ToString())
            .ToImmutableArray();

        var zod = Assert.Single(generated, t => t.Contains("SHALIMAR_TS: shalimar-zod-schemas.g.ts", StringComparison.Ordinal));
        Assert.Contains("import { z } from 'zod'", zod, StringComparison.Ordinal);
        Assert.Contains("export const CreateTaskRequestSchema", zod, StringComparison.Ordinal);
        Assert.Contains(".max(200)", zod, StringComparison.Ordinal);

        var defaults = Assert.Single(generated, t => t.Contains("SHALIMAR_TS: shalimar-defaults.g.ts", StringComparison.Ordinal));
        Assert.Contains("export const CreateTaskRequestDefaults", defaults, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_Emits_UseMutations_Specs()
    {
        var source = """
using System;

namespace Shalimar
{
    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsMutation<TReq, TRes>(this RouteHandlerBuilder builder) => builder;
    }
}

public sealed class RouteHandlerBuilder
{
    public RouteHandlerBuilder WithName(string name) => this;
}

public sealed class WebApplication
{
    public RouteHandlerBuilder MapPost(string pattern, Delegate handler) => new();
}

namespace TestApp;

public static class App
{
    public static void Configure(WebApplication app)
    {
        app.MapPost("/crm/tasks", (CreateTaskRequest req) => new TaskDto(req.Title))
            .AsMutation<CreateTaskRequest, TaskDto>();
    }
}

public sealed record CreateTaskRequest(string Title);
public sealed record TaskDto(string Title);
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
            .Select(t => t.ToString())
            .ToImmutableArray();

        var useMutations = Assert.Single(generated, t => t.Contains("SHALIMAR_TS: shalimar-use-mutations.g.ts", StringComparison.Ordinal));
        Assert.Contains("export function useMutations()", useMutations, StringComparison.Ordinal);
        Assert.Contains("mutationSpecs", useMutations, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_Respects_ForTsxFile_Binding_For_Virtual_Route_Config()
    {
        var source = """
using System;

namespace Shalimar
{
    public interface IComponentProps { }

    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) where TProps : IComponentProps => builder;
        public static RouteHandlerBuilder ForTsxFile(this RouteHandlerBuilder builder, string path) => builder;
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
        app.MapGet("/work", () => new WorkProps("ok"))
            .ForTsxFile("Features/V2/Workbench/WorkbenchPage.tsx")
            .AsComponent<WorkProps>();
    }
}

public sealed record WorkProps(string Message) : Shalimar.IComponentProps;
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
            .Select(t => t.ToString())
            .ToImmutableArray();

        var routes = Assert.Single(generated, t => t.Contains("SHALIMAR_TS: shalimar-routes.g.ts", StringComparison.Ordinal));
        Assert.Contains("route('work', 'Generated/V2Routes/Work/route.tsx')", routes, StringComparison.Ordinal);

        var v2Module = Assert.Single(generated, t => t.Contains("SHALIMAR_TS: Generated/V2Routes/Work/route.tsx", StringComparison.Ordinal));
        Assert.Contains("createFileRoute('/work')", v2Module, StringComparison.Ordinal);
        Assert.Contains("Features/V2/Workbench/WorkbenchPage", v2Module, StringComparison.Ordinal);
    }

    [Fact]
    public void V2_Tree_Missing_Leaf_Binding_Produces_Diagnostic()
    {
        var source = """
using System;

namespace Shalimar
{
    public interface IComponentProps { }
    public sealed record Deferred<T>(string Href);

    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) where TProps : IComponentProps => builder;
        public static RouteHandlerBuilder ForTsxFile(this RouteHandlerBuilder builder, string path) => builder;
        public static RouteHandlerBuilder ForComponent<TProps>(this RouteHandlerBuilder builder) where TProps : IComponentProps => builder;
        public static RouteHandlerBuilder ForNode<TProps>(this RouteHandlerBuilder builder, Func<TProps, object?> node) where TProps : IComponentProps => builder;
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

public sealed record WorkbenchProps(Shalimar.Deferred<GridDto> Grid) : Shalimar.IComponentProps;
public sealed record GridDto(int Total);

public static class App
{
    public static void Configure(WebApplication app)
    {
        // v2 root: opt-in validation via Features/V2/** binding
        app.MapGet("/v2/workbench", () => new WorkbenchProps(new Shalimar.Deferred<GridDto>("/v2/workbench/grid")))
            .ForTsxFile("Features/V2/Workbench/route.tsx")
            .AsComponent<WorkbenchProps>();

        // Intentionally missing the deferred endpoint binding for leaf "Grid".
    }
}
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
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "SHALIMARV2001");
    }

    [Fact]
    public void V2_Tree_With_ForNode_Binding_Passes()
    {
        var source = """
using System;

namespace Shalimar
{
    public interface IComponentProps { }
    public sealed record Deferred<T>(string Href);

    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) where TProps : IComponentProps => builder;
        public static RouteHandlerBuilder ForTsxFile(this RouteHandlerBuilder builder, string path) => builder;
        public static RouteHandlerBuilder ForComponent<TProps>(this RouteHandlerBuilder builder) where TProps : IComponentProps => builder;
        public static RouteHandlerBuilder ForNode<TProps>(this RouteHandlerBuilder builder, Func<TProps, object?> node) where TProps : IComponentProps => builder;
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

public sealed record WorkbenchProps(Shalimar.Deferred<GridDto> Grid) : Shalimar.IComponentProps;
public sealed record GridDto(int Total);

public static class App
{
    public static void Configure(WebApplication app)
    {
        app.MapGet("/v2/workbench", () => new WorkbenchProps(new Shalimar.Deferred<GridDto>("/v2/workbench/grid")))
            .ForTsxFile("Features/V2/Workbench/route.tsx")
            .AsComponent<WorkbenchProps>();

        app.MapGet("/v2/workbench/grid", () => new GridDto(1))
            .ForComponent<WorkbenchProps>()
            .ForNode<WorkbenchProps>(p => p.Grid)
            .AsDeferred<GridDto>();
    }
}
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
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SHALIMARV2001");
    }

    [Fact]
    public void V2_Tree_Nested_Component_Leaf_Uses_Root_ForComponent_Binding()
    {
        var source = """
using System;

namespace Shalimar
{
    public interface IComponentProps { }
    public sealed record Deferred<T>(string Href);
    public sealed record Component<TProps>(TProps Props) where TProps : IComponentProps;

    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) where TProps : IComponentProps => builder;
        public static RouteHandlerBuilder ForTsxFile(this RouteHandlerBuilder builder, string path) => builder;
        public static RouteHandlerBuilder ForComponent<TProps>(this RouteHandlerBuilder builder) where TProps : IComponentProps => builder;
        public static RouteHandlerBuilder ForNode<TProps>(this RouteHandlerBuilder builder, Func<TProps, object?> node) where TProps : IComponentProps => builder;
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

public sealed record WorkbenchProps(Shalimar.Component<AgentPanelProps> AgentPanel) : Shalimar.IComponentProps;
public sealed record AgentPanelProps(Shalimar.Deferred<GridDto> Grid) : Shalimar.IComponentProps;
public sealed record GridDto(int Total);

public static class App
{
    public static void Configure(WebApplication app)
    {
        app.MapGet("/v2/workbench", () => new WorkbenchProps(new Shalimar.Component<AgentPanelProps>(
                new AgentPanelProps(new Shalimar.Deferred<GridDto>("/v2/workbench/grid")))))
            .ForTsxFile("Features/V2/Workbench/route.tsx")
            .AsComponent<WorkbenchProps>();

        // Leaf is nested under WorkbenchProps.AgentPanel.Grid, but binding must still be on the root component type.
        app.MapGet("/v2/workbench/grid", () => new GridDto(1))
            .ForComponent<WorkbenchProps>()
            .ForNode<WorkbenchProps>(p => p.AgentPanel.Grid)
            .AsDeferred<GridDto>();
    }
}
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
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SHALIMARV2001");
    }

    [Fact]
    public void V2_Tree_Duplicate_Leaf_Binding_Produces_Diagnostic()
    {
        var source = """
using System;

namespace Shalimar
{
    public interface IComponentProps { }
    public sealed record Deferred<T>(string Href);

    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) where TProps : IComponentProps => builder;
        public static RouteHandlerBuilder ForTsxFile(this RouteHandlerBuilder builder, string path) => builder;
        public static RouteHandlerBuilder ForComponent<TProps>(this RouteHandlerBuilder builder) where TProps : IComponentProps => builder;
        public static RouteHandlerBuilder ForNode<TProps>(this RouteHandlerBuilder builder, Func<TProps, object?> node) where TProps : IComponentProps => builder;
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

public sealed record WorkbenchProps(Shalimar.Deferred<GridDto> Grid) : Shalimar.IComponentProps;
public sealed record GridDto(int Total);

public static class App
{
    public static void Configure(WebApplication app)
    {
        app.MapGet("/v2/workbench", () => new WorkbenchProps(new Shalimar.Deferred<GridDto>("/v2/workbench/grid")))
            .ForTsxFile("Features/V2/Workbench/route.tsx")
            .AsComponent<WorkbenchProps>();

        // Duplicate bindings for the same leaf.
        app.MapGet("/v2/workbench/grid", () => new GridDto(1))
            .ForComponent<WorkbenchProps>()
            .ForNode<WorkbenchProps>(p => p.Grid)
            .AsDeferred<GridDto>();

        app.MapGet("/v2/workbench/grid2", () => new GridDto(2))
            .ForComponent<WorkbenchProps>()
            .ForNode<WorkbenchProps>(p => p.Grid)
            .AsDeferred<GridDto>();
    }
}
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
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "SHALIMARV2002");
    }

    [Fact]
    public void Generator_Emits_SseRefs_And_Sse_Generic_Type()
    {
        var source = """
using System;

namespace Shalimar
{
    public sealed record Sse<T>(string Href);
    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder AsSse<T>(this RouteHandlerBuilder builder) => builder;
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
        app.MapGet("/", () => new DashboardProps(new Shalimar.Sse<ActivityEvent>("/crm/activity/sse")))
            .AsComponent<DashboardProps>();

        app.MapGet("/crm/activity/sse", () => new ActivityEvent("tick"))
            .AsSse<ActivityEvent>();
    }
}

public sealed record DashboardProps(Shalimar.Sse<ActivityEvent> ActivitySse);

public sealed record ActivityEvent(string Kind);
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

        Assert.Contains(generated, g => g.Text.Contains("public static class SseRefs", StringComparison.Ordinal));
        var shalimarTypes = Assert.Single(generated, g => g.Text.Contains("SHALIMAR_TS: shalimar-types.g.ts", StringComparison.Ordinal)).Text;
        Assert.Contains("export interface Sse<T>", shalimarTypes, StringComparison.Ordinal);
        Assert.Contains("activitySse: Sse<ActivityEvent>", shalimarTypes, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_Emits_Mutations_And_Invalidations()
    {
        var source = """
using System;

namespace Shalimar
{
    public sealed record Deferred<T>(string Href);
    public sealed record Lazy<T>(string Href);
    public static class RouteBuilderExtensions
    {
        public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder AsDeferred<T>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder AsLazy<T>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder ForComponent<TProps>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder AsMutation<TReq, TRes>(this RouteHandlerBuilder builder) => builder;
        public static RouteHandlerBuilder Invalidates<TProps>(this RouteHandlerBuilder builder) => builder;
    }
}

public sealed class RouteHandlerBuilder
{
    public RouteHandlerBuilder WithName(string name) => this;
}

public sealed class WebApplication
{
    public RouteHandlerBuilder MapGet(string pattern, Delegate handler) => new();
    public RouteHandlerBuilder MapPost(string pattern, Delegate handler) => new();
}

namespace TestApp;

public static class App
{
    public static void Configure(WebApplication app)
    {
        app.MapGet("/", () => new DashboardProps(new Shalimar.Deferred<Insights>("/insights"), new Shalimar.Lazy<Forecast>("/forecast")))
            .AsComponent<DashboardProps>();

        app.MapGet("/insights", () => new Insights("x")).ForComponent<DashboardProps>().AsDeferred<Insights>();
        app.MapGet("/forecast", () => new Forecast("y")).ForComponent<DashboardProps>().AsLazy<Forecast>();

        app.MapPost("/do", (DoThing req) => new DoResult("ok"))
            .Invalidates<DashboardProps>()
            .AsMutation<DoThing, DoResult>();
    }
}

public sealed record DashboardProps(Shalimar.Deferred<Insights> Insights, Shalimar.Lazy<Forecast> Forecast);
public sealed record Insights(string Summary);
public sealed record Forecast(string Summary);
public sealed record DoThing(string Name);
public sealed record DoResult(string Status);
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
            .Select(t => new { Text = t.ToString() })
            .ToList();

        Assert.Contains(generated, g => g.Text.Contains("SHALIMAR_TS: shalimar-mutations.g.ts", StringComparison.Ordinal));
        Assert.Contains(generated, g => g.Text.Contains("SHALIMAR_TS: shalimar-invalidations.g.ts", StringComparison.Ordinal));
    }
}
