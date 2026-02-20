using Microsoft.Extensions.Logging;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Domain.Common;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.Core.Infrastructure.Services;

/// <summary>
/// HTTP-based URL expansion service
/// </summary>
public class HttpUrlExpansionService : IUrlExpansionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpUrlExpansionService> _logger;

    public HttpUrlExpansionService(HttpClient httpClient, ILogger<HttpUrlExpansionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanExpand(TikTokUrl url) => url.IsShort;

    public async Task<Result<TikTokUrl>> ExpandUrlAsync(TikTokUrl shortUrl, CancellationToken cancellationToken = default)
    {
        if (!CanExpand(shortUrl))
        {
            return Result<TikTokUrl>.Failure("URL does not require expansion");
        }

        try
        {
            _logger.LogInformation("Expanding short URL: {Url}", shortUrl.Value);

            // Configure request to not follow redirects automatically
            using var request = new HttpRequestMessage(HttpMethod.Head, shortUrl.Value);
            
            // Set a realistic user agent
            request.Headers.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            // Check for redirect
            if (response.Headers.Location != null)
            {
                var expandedUrlString = response.Headers.Location.ToString();
                _logger.LogInformation("Found redirect location: {ExpandedUrl}", expandedUrlString);
                
                // Parse the expanded URL
                var expandedUrlResult = TikTokUrl.Create(expandedUrlString);
                if (expandedUrlResult.IsSuccess)
                {
                    _logger.LogInformation("Successfully expanded and parsed URL");
                    return expandedUrlResult;
                }
                else
                {
                    _logger.LogWarning("Expanded URL is not a valid TikTok URL: {ExpandedUrl}", expandedUrlString);
                    return Result<TikTokUrl>.Failure("Expanded URL is not a valid TikTok URL");
                }
            }
            
            // If no redirect found, the URL might already be expanded or invalid
            _logger.LogWarning("No redirect found for short URL: {Url}", shortUrl.Value);
            return Result<TikTokUrl>.Failure("Unable to expand URL - no redirect found");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while expanding URL: {Url}", shortUrl.Value);
            return Result<TikTokUrl>.Failure($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout while expanding URL: {Url}", shortUrl.Value);
            return Result<TikTokUrl>.Failure("Request timed out");
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while expanding URL: {Url}", shortUrl.Value);
            return Result<TikTokUrl>.Failure($"Unexpected error: {ex.Message}");
        }
    }
}
