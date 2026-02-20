using TikTokDl.Core.Domain.Common;

namespace TikTokDl.Core.Application.Interfaces;

/// <summary>
/// Uses Playwright to automate a browser and scrape TikTok profile pages.
/// </summary>
public interface IBrowserService
{
    /// <summary>
    /// Navigates to a TikTok user profile, scrolls to load all content,
    /// and returns all video/photo post URLs found on the page.
    /// </summary>
    /// <param name="username">TikTok username (without @)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<string[]>> ExtractProfileLinksAsync(
        string username,
        CancellationToken cancellationToken = default);
}
