using Microsoft.Extensions.Logging;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Domain.Common;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.Core.Application.UseCases;

/// <summary>
/// Scrapes all post URLs from a TikTok profile via Playwright, saves them to a
/// link file, then delegates each URL to DownloadFromFileUseCase.
/// Ported from MainForm.cs:MassDownloadByUsername() (line 479).
/// </summary>
public class DownloadByUsernameUseCase
{
    private readonly IBrowserService _browser;
    private readonly DownloadFromFileUseCase _batchDownload;
    private readonly IProgressReporter _progress;
    private readonly ILogger<DownloadByUsernameUseCase> _logger;

    public DownloadByUsernameUseCase(
        IBrowserService browser,
        DownloadFromFileUseCase batchDownload,
        IProgressReporter progress,
        ILogger<DownloadByUsernameUseCase> logger)
    {
        _browser = browser;
        _batchDownload = batchDownload;
        _progress = progress;
        _logger = logger;
    }

    /// <summary>
    /// Scrapes all URLs from the user's profile and downloads them.
    /// </summary>
    /// <param name="usernameOrUrl">Either a bare username or a full tiktok.com/@username URL</param>
    public async Task<Result<BatchDownloadResult>> ExecuteAsync(
        string usernameOrUrl,
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        string username = ExtractUsername(usernameOrUrl);
        if (string.IsNullOrWhiteSpace(username))
            return Result<BatchDownloadResult>.Failure($"Could not determine username from: {usernameOrUrl}");

        _progress.Log($"Scraping profile: @{username}");
        _logger.LogInformation("Starting mass download for @{Username}", username);

        // Step 1: Extract all post URLs via Playwright
        var scrapeResult = await _browser.ExtractProfileLinksAsync(username, cancellationToken);
        if (scrapeResult.IsFailure)
            return Result<BatchDownloadResult>.Failure($"Scrape failed: {scrapeResult.ErrorMessage}");

        var urls = scrapeResult.Value!;
        if (urls.Length == 0)
            return Result<BatchDownloadResult>.Failure($"No posts found for @{username}");

        _progress.Log($"Found {urls.Length} posts for @{username}");

        // Step 2: Save URL list to a links file (enables resume on interruption)
        string linksFile = Path.Combine(options.OutputDirectory, $"{username}_combined_links.txt");
        Directory.CreateDirectory(options.OutputDirectory);
        await File.WriteAllLinesAsync(linksFile, urls, cancellationToken);
        _logger.LogInformation("Saved {Count} links to {File}", urls.Length, linksFile);

        // Step 3: Download all URLs via the batch use case
        return await _batchDownload.ExecuteAsync(linksFile, options, cancellationToken);
    }

    private static string ExtractUsername(string input)
    {
        input = input.Trim();

        // Full URL: https://www.tiktok.com/@username[/...]
        if (input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                input, @"tiktok\.com/@([^/?]+)");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        // @username
        return input.TrimStart('@');
    }
}
