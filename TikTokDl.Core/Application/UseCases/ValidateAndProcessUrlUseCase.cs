using Microsoft.Extensions.Logging;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Domain.Common;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.Core.Application.UseCases;

/// <summary>
/// Use case for validating and processing TikTok URLs
/// </summary>
public class ValidateAndProcessUrlUseCase
{
    private readonly IUrlExpansionService _urlExpansionService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ValidateAndProcessUrlUseCase> _logger;

    public ValidateAndProcessUrlUseCase(
        IUrlExpansionService urlExpansionService,
        INotificationService notificationService,
        ILogger<ValidateAndProcessUrlUseCase> logger)
    {
        _urlExpansionService = urlExpansionService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Validates a URL string and processes it (expanding if needed)
    /// </summary>
    public async Task<Result<ProcessedUrlResult>> ExecuteAsync(
        string urlString, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing URL: {Url}", urlString);

            // Step 1: Parse and validate the URL
            var parseResult = TikTokUrl.Create(urlString);
            if (parseResult.IsFailure)
            {
                _logger.LogWarning("Invalid URL format: {Url}. Error: {Error}", urlString, parseResult.ErrorMessage);
                await _notificationService.ShowNotificationAsync(
                    "Invalid URL", 
                    parseResult.ErrorMessage!, 
                    NotificationType.Warning);
                return Result<ProcessedUrlResult>.Failure(parseResult.ErrorMessage!);
            }

            var originalUrl = parseResult.Value!;
            _logger.LogInformation("Successfully parsed URL. Type: {Type}, Username: {Username}", 
                originalUrl.Type, originalUrl.Username);

            // Step 2: Expand if it's a short URL
            var finalUrl = originalUrl;
            if (originalUrl.RequiresExpansion)
            {
                _logger.LogInformation("Expanding short URL: {Url}", originalUrl.Value);
                
                var expansionResult = await _urlExpansionService.ExpandUrlAsync(originalUrl, cancellationToken);
                if (expansionResult.IsFailure)
                {
                    _logger.LogError("Failed to expand URL: {Url}. Error: {Error}", 
                        originalUrl.Value, expansionResult.ErrorMessage);
                    await _notificationService.ShowNotificationAsync(
                        "URL Expansion Failed", 
                        expansionResult.ErrorMessage!, 
                        NotificationType.Error);
                    return Result<ProcessedUrlResult>.Failure(expansionResult.ErrorMessage!);
                }
                
                finalUrl = expansionResult.Value!;
                _logger.LogInformation("Successfully expanded URL to: {ExpandedUrl}", finalUrl.Value);
            }

            // Step 3: Create result with metadata
            var result = new ProcessedUrlResult(
                OriginalUrl: originalUrl,
                ProcessedUrl: finalUrl,
                WasExpanded: originalUrl.RequiresExpansion,
                IsDownloadable: finalUrl.IsVideo || finalUrl.IsPhotoCarousel,
                IsBatchDownload: finalUrl.IsProfile
            );

            // Step 4: Show success notification
            var notificationMessage = result.IsDownloadable 
                ? $"Ready to download {finalUrl.Type.ToString().ToLower()} from @{finalUrl.Username}"
                : result.IsBatchDownload 
                    ? $"Ready for batch download from @{finalUrl.Username}"
                    : "URL processed successfully";
                    
            await _notificationService.ShowNotificationAsync(
                "URL Validated", 
                notificationMessage, 
                NotificationType.Success);

            _logger.LogInformation("URL processing completed successfully for: {Url}", urlString);
            return Result<ProcessedUrlResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing URL: {Url}", urlString);
            await _notificationService.ShowNotificationAsync(
                "Processing Error",
                "An unexpected error occurred while processing the URL",
                NotificationType.Error);
            return Result<ProcessedUrlResult>.Failure($"Unexpected error: {ex.Message}");
        }
    }
}

/// <summary>
/// Result of URL processing operation
/// </summary>
public record ProcessedUrlResult(
    TikTokUrl OriginalUrl,
    TikTokUrl ProcessedUrl,
    bool WasExpanded,
    bool IsDownloadable,
    bool IsBatchDownload
);
