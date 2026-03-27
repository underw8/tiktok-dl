using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Domain.Common;

namespace TikTokDl.Core.Infrastructure.Services;

/// <summary>
/// Uses Playwright to scrape TikTok profile pages by automating browser scrolling
/// and JavaScript-based link extraction.
/// Ported from MainForm.cs:MassDownloadByUsername() (line 479).
///
/// Cross-platform: no Windows Registry. Browser path is supplied via config or
/// defaults to Playwright's bundled Chromium.
/// </summary>
public class PlaywrightBrowserService : IBrowserService
{
    private readonly string? _customBrowserPath;
    private readonly ILogger<PlaywrightBrowserService> _logger;

    /// <param name="customBrowserPath">
    ///   Optional path to a custom browser executable.
    ///   When null, Playwright's bundled Chromium is used.
    /// </param>
    public PlaywrightBrowserService(
        ILogger<PlaywrightBrowserService> logger,
        string? customBrowserPath = null)
    {
        _logger = logger;
        _customBrowserPath = customBrowserPath;
    }

    public async Task<Result<string[]>> ExtractProfileLinksAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        string profileUrl = $"https://www.tiktok.com/@{username}";
        _logger.LogInformation("Launching browser to scrape @{Username}", username);

        // Single-file publish sets Assembly.Location to "" which causes Playwright's driver
        // locator to NullReferenceException. Set PLAYWRIGHT_DRIVER_PATH to AppContext.BaseDirectory
        // so Playwright finds the driver next to the executable.
        if (string.IsNullOrEmpty(typeof(IPlaywright).Assembly.Location))
        {
            var driverName = OperatingSystem.IsWindows() ? "playwright.cmd" : "playwright.sh";
            var driverPath = Path.Combine(AppContext.BaseDirectory, driverName);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_PATH", driverPath);
            _logger.LogDebug("Single-file mode: set PLAYWRIGHT_DRIVER_PATH={Path}", driverPath);
        }

        IPlaywright playwright;
        try
        {
            playwright = await Playwright.CreateAsync();
        }
        catch (Exception ex) when (ex is NullReferenceException or InvalidOperationException)
        {
            return Result<string[]>.Failure(
                "Playwright failed to initialize — this binary was built as a single-file executable " +
                "which is incompatible with Playwright's driver locator.\n" +
                "Use a build without PublishSingleFile=true, or build from source:\n" +
                "  dotnet run --project TikTokDl.CLI -- download-user <username>");
        }

        var browserType = ResolveBrowserType(playwright, _customBrowserPath);

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = false,
            ExecutablePath = _customBrowserPath
        };

        IBrowser browser;
        try
        {
            browser = await browserType.LaunchAsync(launchOptions);
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
        {
            _logger.LogWarning("Playwright Chromium not found — attempting auto-install");
            Console.WriteLine("[playwright] Chromium not found. Installing (this may take a minute)...");
            var rc = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            if (rc != 0)
                return Result<string[]>.Failure(
                    "Chromium is not installed and auto-install failed.\n" +
                    "Run manually: dotnet tool install -g Microsoft.Playwright.CLI && playwright install chromium");
            browser = await browserType.LaunchAsync(launchOptions);
        }

        try
        {
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            await page.GotoAsync(profileUrl, new PageGotoOptions { Timeout = 120_000 });
            _logger.LogInformation("Navigated to {Url}", profileUrl);

            // Scroll to the bottom repeatedly until page height stabilises
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long before = await page.EvaluateAsync<long>("() => document.body.scrollHeight");
                await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
                await page.WaitForTimeoutAsync(10_000);
                long after = await page.EvaluateAsync<long>("() => document.body.scrollHeight");

                if (after == before) break;
            }

            // Extract video and photo URLs via JS
            var videoUrls = await page.EvaluateAsync<string[]>(
                "() => { var links = document.querySelectorAll('a'); var r = [];" +
                " links.forEach(a => { if (a.href.includes('/video/')) r.push(a.href); }); return r; }");

            var imageUrls = await page.EvaluateAsync<string[]>(
                "() => { var links = document.querySelectorAll('a'); var r = [];" +
                " links.forEach(a => { if (a.href.includes('/photo/')) r.push(a.href); }); return r; }");

            var combined = videoUrls.Concat(imageUrls).Distinct();

            // Filter by username — try @username pattern first, fallback to /username/
            var filtered = combined.Where(u => u.Contains($"/@{username}/")).ToArray();
            if (filtered.Length == 0)
                filtered = combined.Where(u => u.Contains($"/{username}/")).ToArray();

            _logger.LogInformation("Found {Count} links for @{Username}", filtered.Length, username);

            await page.CloseAsync();
            await context.CloseAsync();

            return Result<string[]>.Success(filtered);
        }
        catch (TaskCanceledException)
        {
            return Result<string[]>.Failure("Cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping @{Username}", username);
            return Result<string[]>.Failure($"Browser error: {ex.Message}");
        }
        finally
        {
            await browser.CloseAsync();
            _logger.LogInformation("Browser closed");
        }
    }

    /// <summary>
    /// Determines the Playwright browser type from the executable path.
    /// Defaults to Chromium (cross-platform bundled browser).
    /// </summary>
    private static IBrowserType ResolveBrowserType(IPlaywright playwright, string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
            return playwright.Chromium;

        var lower = executablePath.ToLowerInvariant();
        if (lower.Contains("firefox") || lower.Contains("librewolf"))
            return playwright.Firefox;
        if (lower.Contains("webkit") || lower.Contains("safari"))
            return playwright.Webkit;

        return playwright.Chromium;
    }
}
