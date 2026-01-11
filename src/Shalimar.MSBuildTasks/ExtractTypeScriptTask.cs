using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace Shalimar.MSBuildTasks;

/// <summary>
/// MSBuild task to extract TypeScript from source generator output.
/// The source generator embeds TypeScript in C# files within
/// /* SHALIMAR_TS: filename */ ... /* END_SHALIMAR_TS */ blocks.
/// </summary>
public class ExtractTypeScript : MSBuildTask
{
    private static readonly Regex TsBlockRegex = new Regex(
        @"/\*\s*\nSHALIMAR_TS:\s*(?<filename>[^\s\n]+)\s*\n(?<content>.*?)\nEND_SHALIMAR_TS\s*\n\*/",
        RegexOptions.Singleline | RegexOptions.Compiled);

    [Required]
    public string OutputDirectory { get; set; } = "";

    [Required]
    public string IntermediateDirectory { get; set; } = "";

    public override bool Execute()
    {
        try
        {
            // Ensure output directory exists
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
                Log.LogMessage(MessageImportance.Normal, "Shalimar: Created directory {0}", OutputDirectory);
            }

            var extractedCount = 0;

            // Look in the source generator output directory
            // Source generators output to: obj/Debug/net10.0/generated/Shalimar.SourceGenerator/Shalimar.SourceGenerator.ShalimarGenerator/
            var generatorOutputDir = Path.Combine(
                IntermediateDirectory,
                "generated",
                "Shalimar.SourceGenerator",
                "Shalimar.SourceGenerator.ShalimarGenerator");

            if (Directory.Exists(generatorOutputDir))
            {
                foreach (var csFile in Directory.GetFiles(generatorOutputDir, "*.cs"))
                {
                    var content = File.ReadAllText(csFile);
                    foreach (var block in ParseTypeScriptBlocks(content))
                    {
                        var filename = block.FileName;
                        var tsContent = block.Content;
                        var targetPath = Path.Combine(OutputDirectory, filename);
                        var dir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        // Only write if content changed
                        if (!File.Exists(targetPath) || File.ReadAllText(targetPath) != tsContent)
                        {
                            File.WriteAllText(targetPath, tsContent);
                            Log.LogMessage(MessageImportance.Normal, "Shalimar: Extracted {0}", filename);
                            extractedCount++;
                        }
                    }
                }
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, "Shalimar: Generator output directory not found at {0}", generatorOutputDir);
            }

            // Also check alternate locations (some SDKs use different paths)
            var altPaths = new[]
            {
                Path.Combine(IntermediateDirectory, "generated"),
                IntermediateDirectory
            };

            foreach (var altPath in altPaths)
            {
                if (!Directory.Exists(altPath))
                    continue;

                foreach (var csFile in Directory.GetFiles(altPath, "Shalimar*.g.cs", SearchOption.AllDirectories))
                {
                    var content = File.ReadAllText(csFile);
                    foreach (var block in ParseTypeScriptBlocks(content))
                    {
                        var filename = block.FileName;
                        var tsContent = block.Content;
                        var targetPath = Path.Combine(OutputDirectory, filename);
                        var dir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        if (!File.Exists(targetPath) || File.ReadAllText(targetPath) != tsContent)
                        {
                            File.WriteAllText(targetPath, tsContent);
                            Log.LogMessage(MessageImportance.Normal, "Shalimar: Extracted {0}", filename);
                            extractedCount++;
                        }
                    }
                }
            }

            if (extractedCount > 0)
            {
                Log.LogMessage(MessageImportance.High, "Shalimar: Extracted {0} TypeScript file(s) to {1}", extractedCount, OutputDirectory);
            }
            else
            {
                // This is normal on first build before source generator runs
                Log.LogMessage(MessageImportance.Normal, "Shalimar: No TypeScript files to extract (this is normal on first build)");
            }

            return true;
        }
        catch (System.Exception ex)
        {
            Log.LogError("Shalimar: Failed to extract TypeScript: {0}", ex.Message);
            return false;
        }
    }

    internal static IReadOnlyList<(string FileName, string Content)> ParseTypeScriptBlocks(string csContent)
    {
        if (string.IsNullOrEmpty(csContent))
            return Array.Empty<(string, string)>();

        // Normalize CRLF to LF to keep the regex simple and deterministic.
        var normalized = csContent.Replace("\r\n", "\n");

        var matches = TsBlockRegex.Matches(normalized);
        if (matches.Count == 0)
            return Array.Empty<(string, string)>();

        var blocks = new List<(string FileName, string Content)>(matches.Count);
        foreach (Match match in matches)
        {
            var filename = match.Groups["filename"].Value;
            var tsContent = match.Groups["content"].Value.Trim();

            if (!string.IsNullOrWhiteSpace(filename))
                blocks.Add((filename, tsContent));
        }

        return blocks;
    }
}
