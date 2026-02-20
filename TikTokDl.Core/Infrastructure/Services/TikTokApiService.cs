using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Domain.Common;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.Core.Infrastructure.Services;

/// <summary>
/// Fetches SD-quality media from the unofficial TikTok internal API.
/// Ported from MainForm.cs: GetMedia() (line 1144), GetMediaID() (line 1034),
/// GetMediaUrl() (line 975), GetRedirectUrl() (line 1117).
/// Note: This API is capped at SD quality and ~1 request every 1-30 seconds.
/// </summary>
public class TikTokApiService : IMediaApiService
{
    private const string ApiBaseUrl =
        "https://api22-normal-c-alisg.tiktokv.com/aweme/v1/feed/?" +
        "aweme_id={0}&iid=7238789370386695942&device_id=7238787983025079814" +
        "&resolution=1080*2400&channel=googleplay&app_name=musical_ly" +
        "&version_code=350103&device_platform=android&device_type=Pixel+7&os_version=13";

    private readonly ILogger<TikTokApiService> _logger;

    public TikTokApiService(ILogger<TikTokApiService> logger)
    {
        _logger = logger;
    }

    public async Task<Result<VideoData>> GetMediaAsync(
        string mediaId,
        bool withWatermark,
        CancellationToken cancellationToken = default)
    {
        var apiUrl = string.Format(ApiBaseUrl, mediaId);

        using var client = new HttpClient();
        const int max429Retries = 5;
        int retryDelay = 5_000;

        for (int attempt = 1; attempt <= max429Retries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Options, apiUrl);
                var response = await client.SendAsync(request, cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt == max429Retries)
                        return Result<VideoData>.Failure(
                            $"HTTP 429 TooManyRequests after {max429Retries} retries for {mediaId}");
                    _logger.LogWarning(
                        "HTTP 429 for {MediaId}. Waiting {Delay}ms (attempt {Attempt}/{Max})",
                        mediaId, retryDelay, attempt, max429Retries);
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay = Math.Min(retryDelay * 2, 60_000); // exponential backoff, cap at 60s
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(json))
                    return Result<VideoData>.Failure($"Empty API response for media ID {mediaId}");

                var data = JsonConvert.DeserializeObject<ApiData>(json);
                if (data?.aweme_list == null || data.aweme_list.Count == 0)
                    return Result<VideoData>.Failure($"No aweme_list in API response for {mediaId}");

                var item = data.aweme_list.FirstOrDefault();
                if (item?.aweme_id != mediaId)
                    return Result<VideoData>.Failure($"Media ID mismatch in API response for {mediaId}");

                var videoUrl = withWatermark
                    ? item.video?.download_addr?.url_list.FirstOrDefault()
                    : item.video?.play_addr?.url_list.FirstOrDefault();

                var imageUrls = item.image_post_info?.images?
                    .Select(img => img.display_image.url_list.FirstOrDefault() ?? string.Empty)
                    .Where(u => !string.IsNullOrEmpty(u))
                    .ToList() ?? [];

                return Result<VideoData>.Success(new VideoData
                {
                    Url = videoUrl ?? string.Empty,
                    Images = imageUrls,
                    Id = mediaId,
                    AvatarUrls = item.author?.avatar_medium?.url_list ?? [],
                    GifAvatarUrls = item.author?.video_icon?.url_list ?? [],
                    Name = item.author?.unique_Id ?? string.Empty,
                });
            }
            catch (TaskCanceledException)
            {
                return Result<VideoData>.Failure("Download cancelled");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error for media {MediaId}", mediaId);
                return Result<VideoData>.Failure($"HTTP error: {ex.Message}");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parse error for media {MediaId}", mediaId);
                return Result<VideoData>.Failure($"JSON parse error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error for media {MediaId}", mediaId);
                return Result<VideoData>.Failure($"Unexpected error: {ex.Message}");
            }
        }

        return Result<VideoData>.Failure($"Unexpected retry loop exit for {mediaId}");
    }

    /// <summary>
    /// Extracts a 19-character numeric media ID from a TikTok URL.
    /// Handles short URL resolution, /video/ and /photo/ paths.
    /// Ported from MainForm.cs:GetMediaID() (line 1034).
    /// </summary>
    public async Task<Result<string>> ExtractMediaIdAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // /t/ short links need a full HTTP GET to resolve
            if (url.Contains("/t/"))
            {
                using var client = new HttpClient();
                var response = await client.GetAsync(url, cancellationToken);
                url = response.RequestMessage?.RequestUri?.Segments.LastOrDefault()?.TrimEnd('/')
                      ?? string.Empty;
            }
            else
            {
                url = await ResolveRedirectAsync(url, cancellationToken);
            }

            bool isVideo = url.Contains("/video/");
            bool isPhoto = url.Contains("/photo/");

            if (!isVideo && !isPhoto)
                return Result<string>.Failure($"URL does not contain /video/ or /photo/: {url}");

            int startIndex = isPhoto
                ? url.IndexOf("/photo/") + 7
                : url.IndexOf("/video/") + 7;

            int endIndex = startIndex + 19;
            if (endIndex > url.Length) endIndex = startIndex + 18;
            if (endIndex > url.Length || endIndex <= startIndex)
                return Result<string>.Failure($"Invalid URL format: {url}");

            return Result<string>.Success(url.Substring(startIndex, endIndex - startIndex));
        }
        catch (TaskCanceledException)
        {
            return Result<string>.Failure("Cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting media ID from {Url}", url);
            return Result<string>.Failure($"Error extracting media ID: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the video/photo ID from a URL, or returns the resolved URL as fallback.
    /// Ported from MainForm.cs:GetMediaUrl() (line 975).
    /// Used by HD services that accept either a media ID or full URL.
    /// </summary>
    public async Task<Result<string>> GetMediaUrlOrIdAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        string resolved = url;

        if (url.Contains("vm.tiktok.com") || url.Contains("vt.tiktok.com"))
            resolved = await ResolveRedirectAsync(url, cancellationToken);

        var photoMatch = Regex.Match(resolved, @"/photo/(\d+)");
        if (photoMatch.Success) return Result<string>.Success(photoMatch.Groups[1].Value);

        var videoMatch = Regex.Match(resolved, @"/video/(\d+)");
        if (videoMatch.Success) return Result<string>.Success(videoMatch.Groups[1].Value);

        return Result<string>.Success(resolved);
    }

    /// <summary>
    /// Follows HTTP redirects to get the final URL.
    /// Ported from MainForm.cs:GetRedirectUrl() (line 1117).
    /// </summary>
    public async Task<string> ResolveRedirectAsync(string url, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return response.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
    }

    /// <summary>
    /// Extracts the @username from a TikTok URL.
    /// Ported from MainForm.cs:ExtractUsernameFromUrl() (line 2166).
    /// </summary>
    public async Task<string> ExtractUsernameAsync(string url, CancellationToken cancellationToken = default)
    {
        if (url.Contains("vm.tiktok.com") || url.Contains("vt.tiktok.com"))
            url = await ResolveRedirectAsync(url, cancellationToken);

        var match = Regex.Match(url, @"tiktok\.com/@([\w.]+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    // JSON deserialization models — private, mirroring the original MainForm nested classes

    private class ApiData
    {
        public List<Aweme> aweme_list { get; set; } = [];
    }

    private class Aweme
    {
        public string aweme_id { get; set; } = string.Empty;
        public ImagePostInfo? image_post_info { get; set; }
        public VideoAddr? video { get; set; }
        public AuthorInfo? author { get; set; }
    }

    private class ImagePostInfo
    {
        public List<ImageItem> images { get; set; } = [];
    }

    private class ImageItem
    {
        public DisplayImage display_image { get; set; } = new();
    }

    private class DisplayImage
    {
        public List<string> url_list { get; set; } = [];
    }

    private class VideoAddr
    {
        public UrlList? download_addr { get; set; }
        public UrlList? play_addr { get; set; }
    }

    private class AuthorInfo
    {
        public UrlList? avatar_medium { get; set; }
        public UrlList? video_icon { get; set; }
        public string unique_Id { get; set; } = string.Empty;
    }

    private class UrlList
    {
        public List<string> url_list { get; set; } = [];
    }
}
