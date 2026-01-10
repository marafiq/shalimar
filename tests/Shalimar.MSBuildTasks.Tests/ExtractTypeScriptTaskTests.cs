using Shalimar.MSBuildTasks;
using Xunit;

namespace Shalimar.MSBuildTasks.Tests;

public class ExtractTypeScriptTaskTests
{
    [Fact]
    public void ParseTypeScriptBlocks_ReturnsEmpty_ForNoBlocks()
    {
        var blocks = ExtractTypeScript.ParseTypeScriptBlocks("public class C {}");
        Assert.Empty(blocks);
    }

    [Fact]
    public void ParseTypeScriptBlocks_ParsesSingleBlock_AndTrimsContent()
    {
        var input = """
            /* 
            SHALIMAR_TS: foo.ts
              export const x = 1;
            
            END_SHALIMAR_TS
            */
            """;

        var blocks = ExtractTypeScript.ParseTypeScriptBlocks(input);

        Assert.Single(blocks);
        Assert.Equal("foo.ts", blocks[0].FileName);
        Assert.Equal("export const x = 1;", blocks[0].Content);
    }

    [Fact]
    public void ParseTypeScriptBlocks_ParsesMultipleBlocks()
    {
        var input = """
            /*
            SHALIMAR_TS: a.ts
            export const a = "a";
            END_SHALIMAR_TS
            */
            // other code
            /*
            SHALIMAR_TS: b.ts
            export const b = "b";
            END_SHALIMAR_TS
            */
            """;

        var blocks = ExtractTypeScript.ParseTypeScriptBlocks(input);

        Assert.Equal(2, blocks.Count);
        Assert.Equal(("a.ts", "export const a = \"a\";"), blocks[0]);
        Assert.Equal(("b.ts", "export const b = \"b\";"), blocks[1]);
    }

    [Fact]
    public void ParseTypeScriptBlocks_SupportsWindowsLineEndings()
    {
        var input = "/*\r\nSHALIMAR_TS: win.ts\r\nexport const ok = true;\r\nEND_SHALIMAR_TS\r\n*/";
        var blocks = ExtractTypeScript.ParseTypeScriptBlocks(input);

        Assert.Single(blocks);
        Assert.Equal("win.ts", blocks[0].FileName);
        Assert.Equal("export const ok = true;", blocks[0].Content);
    }
}

