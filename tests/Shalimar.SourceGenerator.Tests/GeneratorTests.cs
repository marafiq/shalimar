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

    // TODO: Add snapshot tests with Verify.Xunit for generated code
}
