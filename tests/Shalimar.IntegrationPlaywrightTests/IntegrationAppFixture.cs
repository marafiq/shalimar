using Microsoft.Playwright;
using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

public class IntegrationAppFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl => Environment.GetEnvironmentVariable("INTEGRATION_APP_URL")
        ?? "http://localhost:5099";

    public string ArtifactsRoot { get; } = GetArtifactsRoot();

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task<(IBrowserContext Context, IPage Page, PlaywrightDiagnostics Diagnostics)> NewPageAsync(string testName)
    {
        Directory.CreateDirectory(ArtifactsRoot);

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            Locale = "en-US",
            ColorScheme = ColorScheme.Light,
            ReducedMotion = ReducedMotion.Reduce,
            DeviceScaleFactor = 1,
            ViewportSize = new ViewportSize
            {
                Width = 1280,
                Height = 720
            }
        });

        var page = await context.NewPageAsync();
        await page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Content = """
                      *,
                      *::before,
                      *::after {
                        animation-duration: 0s !important;
                        animation-delay: 0s !important;
                        transition-duration: 0s !important;
                        transition-delay: 0s !important;
                        caret-color: transparent !important;
                      }
                      """
        });
        var diagnostics = new PlaywrightDiagnostics(ArtifactsRoot, testName);
        diagnostics.Attach(page);

        return (context, page, diagnostics);
    }

    public async Task<string> GetHtmlAsync(string path = "/")
    {
        using var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        return await client.GetStringAsync(path);
    }

    public async Task DisposeAsync()
    {
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    private static string GetArtifactsRoot()
    {
        var env = Environment.GetEnvironmentVariable("SHALIMAR_TEST_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        // Keep this under tests/ so `dotnet test` artifacts are easy to locate and can be ignored by git.
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestResults", "playwright"));
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture> { }

public sealed class PlaywrightDiagnostics
{
    private readonly string _root;
    private readonly string _testName;
    private readonly List<string> _console = new();
    private readonly List<string> _pageErrors = new();

    public PlaywrightDiagnostics(string root, string testName)
    {
        _root = root;
        _testName = Sanitize(testName);
    }

    public void Attach(IPage page)
    {
        page.Console += (_, msg) =>
        {
            var line = $"[{DateTimeOffset.UtcNow:O}] console.{msg.Type}: {msg.Text}";
            _console.Add(line);
        };

        page.PageError += (_, err) =>
        {
            var line = $"[{DateTimeOffset.UtcNow:O}] pageerror: {err}";
            _pageErrors.Add(line);
        };
    }

    public async Task FlushAsync()
    {
        var testDir = Path.Combine(_root, _testName);
        Directory.CreateDirectory(testDir);

        await File.WriteAllLinesAsync(Path.Combine(testDir, "console.log"), _console);
        await File.WriteAllLinesAsync(Path.Combine(testDir, "page-errors.log"), _pageErrors);
    }

    public async Task CaptureFailureAsync(IPage page, Exception ex)
    {
        var testDir = Path.Combine(_root, _testName);
        Directory.CreateDirectory(testDir);

        await FlushAsync();

        await File.WriteAllTextAsync(Path.Combine(testDir, "exception.txt"), ex.ToString());

        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(testDir, "failure.png"),
                FullPage = true
            });
        }
        catch
        {
            // Best effort; don't hide the real test failure.
        }

        try
        {
            var html = await page.ContentAsync();
            await File.WriteAllTextAsync(Path.Combine(testDir, "failure.html"), html);
        }
        catch
        {
            // Best effort; don't hide the real test failure.
        }
    }

    public async Task CaptureSnapshotAsync(IPage page, string fileName)
    {
        var testDir = Path.Combine(_root, _testName);
        Directory.CreateDirectory(testDir);

        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(testDir, fileName),
                FullPage = true
            });
        }
        catch
        {
            // Best effort.
        }
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}

public static class SnapshotAssertions
{
    public static async Task AssertMatchesAsync(IPage page, PlaywrightDiagnostics diag, string testName, string snapshotFileName)
    {
        var repoRoot = FindRepoRoot();
        var snapshotsDir = Path.Combine(repoRoot, "tests", "Shalimar.IntegrationPlaywrightTests", "Snapshots");
        Directory.CreateDirectory(snapshotsDir);

        var baselinePath = Path.Combine(snapshotsDir, snapshotFileName);
        var update = string.Equals(Environment.GetEnvironmentVariable("SHALIMAR_UPDATE_SNAPSHOTS"), "1", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(Environment.GetEnvironmentVariable("SHALIMAR_UPDATE_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase);

        var actualBytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true });

        if (!File.Exists(baselinePath) || update)
        {
            await File.WriteAllBytesAsync(baselinePath, actualBytes);
            return;
        }

        var expectedBytes = await File.ReadAllBytesAsync(baselinePath);
        if (actualBytes.SequenceEqual(expectedBytes))
            return;

        await diag.CaptureSnapshotAsync(page, "actual.png");
        throw new Xunit.Sdk.XunitException(
            $"Snapshot mismatch for '{testName}'.\n" +
            $"Baseline: {baselinePath}\n" +
            $"Actual:   {Path.Combine(diagRoot(diag, testName), "actual.png")}\n" +
            "To update snapshots, run with SHALIMAR_UPDATE_SNAPSHOTS=1.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && dir != null; i++)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }

        // Fall back to current directory (works when tests are run from repo root).
        return Directory.GetCurrentDirectory();
    }

    private static string diagRoot(PlaywrightDiagnostics diag, string testName)
    {
        // Mirror PlaywrightDiagnostics directory structure.
        var root = (string)typeof(PlaywrightDiagnostics)
            .GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(diag)!;

        var sanitized = (string)typeof(PlaywrightDiagnostics)
            .GetMethod("Sanitize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { testName })!;

        return Path.Combine(root, sanitized);
    }
}
