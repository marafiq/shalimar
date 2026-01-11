using Shalimar.Vite;
using Xunit;

namespace Shalimar.Vite.Tests;

public class ViteProxyDecisionTests
{
    [Theory]
    [InlineData("/__vite_ping")]
    [InlineData("/@vite/client")]
    [InlineData("/@fs/some/path")]
    [InlineData("/Features/App/main.tsx")]
    [InlineData("/Features/Home/index.tsx")]
    [InlineData("/Generated/routeTree.gen.ts")]
    public void ShouldProxyToVite_ReturnsTrue_ForKnownDevPaths(string path)
    {
        var options = new ViteOptions();
        Assert.True(ViteProxyDecision.ShouldProxyToVite(path, options));
    }

    [Theory]
    [InlineData("/foo.tsx")]
    [InlineData("/foo.ts")]
    [InlineData("/foo.jsx")]
    [InlineData("/foo.js")]
    [InlineData("/foo.css")]
    public void ShouldProxyToVite_ReturnsTrue_ForSourceFilesOutsideDist(string path)
    {
        var options = new ViteOptions();
        Assert.True(ViteProxyDecision.ShouldProxyToVite(path, options));
    }

    [Theory]
    [InlineData("/dist/assets/app.js")]
    [InlineData("/dist/style.css")]
    public void ShouldProxyToVite_ReturnsFalse_ForDistAssets(string path)
    {
        var options = new ViteOptions();
        Assert.False(ViteProxyDecision.ShouldProxyToVite(path, options));
    }
}

