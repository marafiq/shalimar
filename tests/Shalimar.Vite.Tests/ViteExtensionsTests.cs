using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Shalimar.Vite;
using Xunit;

namespace Shalimar.Vite.Tests;

public class ViteExtensionsTests
{
    [Fact]
    public void UseShalimarVite_InDevelopment_Throws_WhenOptionsNotRegistered()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        var env = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        Assert.Throws<InvalidOperationException>(() => app.UseShalimarVite(env));
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "/tmp";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

