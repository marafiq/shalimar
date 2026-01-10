using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Shalimar.Runtime.Tests;

public class ShalimarExtensionsTests
{
    [Fact]
    public void AddShalimar_RegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddShalimar<TestContext>(options =>
        {
            options.ContextFactory = (sp, http) => new TestContext { Value = "test" };
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<ShalimarOptions<TestContext>>();

        Assert.NotNull(options);
        Assert.NotNull(options.ContextFactory);
    }

    [Fact]
    public void AddShalimar_RegistersShellRenderer()
    {
        // This test can only verify that the service is registered
        // Actually resolving ShellRenderer requires IWebHostEnvironment
        var services = new ServiceCollection();

        services.AddShalimar<TestContext>();

        var provider = services.BuildServiceProvider();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ShellRenderer));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddShalimar_WorksWithoutConfigure()
    {
        var services = new ServiceCollection();

        services.AddShalimar<TestContext>();

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<ShalimarOptions<TestContext>>();

        Assert.NotNull(options);
        Assert.Null(options.ContextFactory); // Should be null when not configured
    }

    private record TestContext
    {
        public string Value { get; init; } = "";
    }
}
