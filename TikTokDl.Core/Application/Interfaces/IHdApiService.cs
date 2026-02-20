using TikTokDl.Core.Domain.Common;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.Core.Application.Interfaces;

/// <summary>
/// Fetches HD-quality media via the tikwm.com third-party API.
/// </summary>
public interface IHdApiService
{
    /// <summary>
    /// Downloads HD images for a photo carousel post.
    /// </summary>
    /// <param name="mediaId">TikTok media ID or full URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<HdImageData>> GetHdImagesAsync(
        string mediaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits and polls a task for an HD video download URL.
    /// Two-step process: submit → poll for readiness.
    /// </summary>
    /// <param name="mediaId">TikTok media ID or full URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<HdVideoData>> GetHdVideoAsync(
        string mediaId,
        CancellationToken cancellationToken = default);
}

/// <summary>HD image data returned by tikwm.com</summary>
public record HdImageData(string Username, List<string> ImageUrls, string MediaId);

/// <summary>HD video data returned by tikwm.com</summary>
public record HdVideoData(string Username, string VideoUrl, string VideoId, long SizeBytes);
