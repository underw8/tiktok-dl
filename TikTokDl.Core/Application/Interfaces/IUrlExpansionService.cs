using TikTokDl.Core.Domain.Common;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.Core.Application.Interfaces;

/// <summary>
/// Service for expanding short URLs to their full form
/// </summary>
public interface IUrlExpansionService
{
    /// <summary>
    /// Expands a short TikTok URL to its full form
    /// </summary>
    /// <param name="shortUrl">The short URL to expand</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The expanded URL or failure</returns>
    Task<Result<TikTokUrl>> ExpandUrlAsync(TikTokUrl shortUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the service can expand the given URL
    /// </summary>
    bool CanExpand(TikTokUrl url);
}
