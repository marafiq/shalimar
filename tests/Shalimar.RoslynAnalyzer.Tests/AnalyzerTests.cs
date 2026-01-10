using Shalimar.RoslynAnalyzer;
using Xunit;

namespace Shalimar.RoslynAnalyzer.Tests;

public class AnalyzerTests
{
    [Fact]
    public void Analyzer_HasMissingPropsTypeId()
    {
        Assert.Equal("SHALIMAR001", ShalimarAnalyzer.MissingPropsTypeId);
    }

    [Fact]
    public void Analyzer_HasInvalidRoutePathId()
    {
        Assert.Equal("SHALIMAR002", ShalimarAnalyzer.InvalidRoutePathId);
    }

    [Fact]
    public void Analyzer_HasDuplicateRouteId()
    {
        Assert.Equal("SHALIMAR003", ShalimarAnalyzer.DuplicateRouteId);
    }

    [Fact]
    public void Analyzer_HasMissingJsxFileId()
    {
        Assert.Equal("SHALIMAR004", ShalimarAnalyzer.MissingJsxFileId);
    }

    [Fact]
    public void Analyzer_SupportsFourDiagnostics()
    {
        var analyzer = new ShalimarAnalyzer();
        Assert.Equal(4, analyzer.SupportedDiagnostics.Length);
    }

    [Fact]
    public void Analyzer_AllDiagnosticsAreEnabled()
    {
        var analyzer = new ShalimarAnalyzer();
        foreach (var diagnostic in analyzer.SupportedDiagnostics)
        {
            Assert.True(diagnostic.IsEnabledByDefault, $"Diagnostic {diagnostic.Id} should be enabled by default");
        }
    }

    [Fact]
    public void Analyzer_DiagnosticsCategoryIsShalimar()
    {
        var analyzer = new ShalimarAnalyzer();
        foreach (var diagnostic in analyzer.SupportedDiagnostics)
        {
            Assert.Equal("Shalimar", diagnostic.Category);
        }
    }
}
