using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace Shalimar.SandboxApp.Tests;

public sealed class SandboxAppFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        // The repo test script starts the app and sets INTEGRATION_APP_URL.
        // This keeps the app lifecycle deterministic and non-parallel.
        BaseUrl = Environment.GetEnvironmentVariable("INTEGRATION_APP_URL") ?? "http://localhost:5099";

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    public async Task ResetAsync()
    {
        using var http = new HttpClient();
        var res = await http.PostAsync($"{BaseUrl}/test/reset", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        res.EnsureSuccessStatusCode();
    }

    public async Task<(IBrowserContext Context, IPage Page)> NewPageAsync()
    {
        if (_browser is null) throw new InvalidOperationException("Fixture not initialized.");
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });
        var page = await context.NewPageAsync();
        return (context, page);
    }
}

[CollectionDefinition("Sandbox")]
public sealed class SandboxCollection : ICollectionFixture<SandboxAppFixture> { }

