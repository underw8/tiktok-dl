using TikTokDl.Core.Domain.Common;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.Core.Application.Interfaces;

/// <summary>
/// Fetches SD-quality media metadata from the official TikTok API endpoint.
/// Capped at ~1 req/1-30 seconds by the upstream API.
/// </summary>
public interface IMediaApiService
{
    /// <summary>
    /// Resolves a media ID to its download data via the TikTok internal API.
    /// </summary>
    /// <param name="mediaId">19-digit numeric TikTok media ID</param>
    /// <param name="withWatermark">Return the watermarked download URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<VideoData>> GetMediaAsync(
        string mediaId,
        bool withWatermark,
        CancellationToken cancellationToken = default);
}
