using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Domain.Common;

namespace TikTokDl.Core.Infrastructure.Services;

/// <summary>
/// Fetches HD-quality media via the tikwm.com third-party API.
/// Ported from MainForm.cs:HDImageDownload() (line 1318) and HDVideoDownload() (line 1527).
/// Note: Subject to a daily 5,000-request cap on tikwm.com.
/// </summary>
public class TikWmApiService : IHdApiService
{
    private const string ImageApiEndpoint = "https://www.tikwm.com/api/";
    private const string VideoSubmitEndpoint = "https://www.tikwm.com/api/video/task/submit";
    private const string VideoResultEndpointTemplate = "https://www.tikwm.com/api/video/task/result?task_id={0}";
    private const int MaxRetries = 5;
    private const int PollRetries = 15;
    private const int PollDelayMs = 500;

    private readonly ILogger<TikWmApiService> _logger;

    public TikWmApiService(ILogger<TikWmApiService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// GET https://www.tikwm.com/api/?url={mediaId}&hd=1
    /// Returns image URLs for photo carousel posts.
    /// </summary>
    public async Task<Result<HdImageData>> GetHdImagesAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        int attempt = 0;

        while (attempt < MaxRetries)
        {
            try
            {
                var url = $"{ImageApiEndpoint}?url={mediaId}&hd=1";
                var response = await client.GetAsync(url, cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    attempt++;
                    _logger.LogWarning("tikwm.com images: HTTP 429, attempt {Attempt}/{Max}", attempt, MaxRetries);
                    await Task.Delay(10_000, cancellationToken); // longer wait for rate limiting
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    attempt++;
                    _logger.LogWarning("tikwm.com images: HTTP {Status}, attempt {Attempt}/{Max}",
                        response.StatusCode, attempt, MaxRetries);
                    await Task.Delay(2000, cancellationToken);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                dynamic data = JsonConvert.DeserializeObject(body)!;

                if (data.code != 0)
                {
                    attempt++;
                    _logger.LogWarning("tikwm.com returned non-zero code for {MediaId}, attempt {Attempt}/{Max}",
                        mediaId, attempt, MaxRetries);
                    await Task.Delay(2000, cancellationToken);
                    continue;
                }

                string username = (string)data.data.author.unique_id ?? string.Empty;
                var imageList = new List<string>();

                if (data.data.images != null)
                {
                    foreach (var img in data.data.images)
                        imageList.Add((string)img);
                }

                if (imageList.Count == 0)
                    return Result<HdImageData>.Failure($"No images found for media {mediaId}");

                return Result<HdImageData>.Success(new HdImageData(username, imageList, mediaId));
            }
            catch (TaskCanceledException)
            {
                return Result<HdImageData>.Failure("Cancelled");
            }
            catch (HttpRequestException ex)
            {
                attempt++;
                _logger.LogWarning(ex, "HTTP error on tikwm.com images, attempt {Attempt}/{Max}", attempt, MaxRetries);
                if (attempt >= MaxRetries)
                    return Result<HdImageData>.Failure($"HTTP error after {MaxRetries} attempts: {ex.Message}");
                await Task.Delay(5000, cancellationToken);
            }
        }

        return Result<HdImageData>.Failure($"Failed after {MaxRetries} attempts");
    }

    /// <summary>
    /// Two-step: POST to submit endpoint → poll result endpoint until status=2.
    /// Returns HD video URL for download.
    /// </summary>
    public async Task<Result<HdVideoData>> GetHdVideoAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        int attempt = 0;

        while (attempt < MaxRetries)
        {
            try
            {
                // Step 1: Submit task
                var submitContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "url", mediaId },
                    { "web", "1" }
                });
                var submitResponse = await client.PostAsync(VideoSubmitEndpoint, submitContent, cancellationToken);
                if (submitResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    attempt++;
                    _logger.LogWarning("tikwm.com video submit: HTTP 429, attempt {Attempt}/{Max}", attempt, MaxRetries);
                    await Task.Delay(10_000, cancellationToken);
                    continue;
                }
                submitResponse.EnsureSuccessStatusCode();

                var submitBody = await submitResponse.Content.ReadAsStringAsync(cancellationToken);
                dynamic submitData = JsonConvert.DeserializeObject(submitBody)!;

                if (submitData.code != 0 || submitData.data?.task_id == null)
                    return Result<HdVideoData>.Failure($"Task submission failed: {submitBody}");

                string taskId = (string)submitData.data.task_id;
                var resultEndpoint = string.Format(VideoResultEndpointTemplate, taskId);

                // Step 2: Poll for readiness
                dynamic? resultData = null;
                bool isReady = false;

                for (int poll = 0; poll < PollRetries; poll++)
                {
                    var resultResponse = await client.GetAsync(resultEndpoint, cancellationToken);
                    if (resultResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("tikwm.com video poll: HTTP 429, retrying poll in 10s");
                        await Task.Delay(10_000, cancellationToken);
                        continue; // retry this poll iteration, task_id is preserved
                    }
                    resultResponse.EnsureSuccessStatusCode();
                    var resultBody = await resultResponse.Content.ReadAsStringAsync(cancellationToken);
                    resultData = JsonConvert.DeserializeObject(resultBody);

                    if (resultData?.code == 0 && resultData?.data != null)
                    {
                        int status = (int)resultData!.data.status;
                        long size = (long)resultData.data.detail.size;
                        if (status == 2 && size > 0)
                        {
                            isReady = true;
                            break;
                        }
                    }

                    await Task.Delay(PollDelayMs, cancellationToken);
                }

                if (!isReady || resultData == null)
                    return Result<HdVideoData>.Failure($"Task not ready after {PollRetries} polls");

                if (resultData!.code != 0)
                {
                    attempt++;
                    _logger.LogWarning("tikwm.com video: non-zero result code for {MediaId}, attempt {Attempt}/{Max}",
                        mediaId, attempt, MaxRetries);
                    await Task.Delay(2000, cancellationToken);
                    continue;
                }

                string videoUrl = (string)resultData.data.detail.play_url;
                long sizeBytes = (long)resultData.data.detail.size;
                string username = (string)resultData.data.detail.author.unique_id ?? string.Empty;

                if (string.IsNullOrEmpty(videoUrl) || sizeBytes == 0)
                {
                    attempt++;
                    await Task.Delay(2000, cancellationToken);
                    continue;
                }

                // Extract video ID from mediaId URL if possible
                var videoIdMatch = System.Text.RegularExpressions.Regex.Match(
                    mediaId, @"(?:video/)(\d+)");
                string videoId = videoIdMatch.Success ? videoIdMatch.Groups[1].Value : mediaId;

                return Result<HdVideoData>.Success(new HdVideoData(username, videoUrl, videoId, sizeBytes));
            }
            catch (TaskCanceledException)
            {
                return Result<HdVideoData>.Failure("Cancelled");
            }
            catch (HttpRequestException ex)
            {
                attempt++;
                _logger.LogWarning(ex, "HTTP error on tikwm.com video, attempt {Attempt}/{Max}", attempt, MaxRetries);
                if (attempt >= MaxRetries)
                    return Result<HdVideoData>.Failure($"HTTP error after {MaxRetries} attempts: {ex.Message}");
                await Task.Delay(5000, cancellationToken);
            }
        }

        return Result<HdVideoData>.Failure($"Failed after {MaxRetries} attempts");
    }
}
