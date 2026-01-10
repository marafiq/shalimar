using Microsoft.Playwright;
using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

public class IntegrationAppFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl => Environment.GetEnvironmentVariable("INTEGRATION_APP_URL")
        ?? "http://localhost:5099";

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task<IPage> NewPageAsync() =>
        await (await _browser!.NewContextAsync()).NewPageAsync();

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
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture> { }
