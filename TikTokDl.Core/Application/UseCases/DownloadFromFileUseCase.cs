using Microsoft.Extensions.Logging;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Domain.Common;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.Core.Application.UseCases;

/// <summary>
/// Reads a line-separated list of TikTok URLs from a text file and downloads each one.
/// Ported from MainForm.cs:DownloadFromTextFile() and HDDownloadFromTextFile().
/// </summary>
public class DownloadFromFileUseCase
{
    private readonly DownloadSingleMediaUseCase _singleDownload;
    private readonly IProgressReporter _progress;
    private readonly ILogger<DownloadFromFileUseCase> _logger;

    public DownloadFromFileUseCase(
        DownloadSingleMediaUseCase singleDownload,
        IProgressReporter progress,
        ILogger<DownloadFromFileUseCase> logger)
    {
        _singleDownload = singleDownload;
        _progress = progress;
        _logger = logger;
    }

    /// <summary>
    /// Reads all URLs from <paramref name="filePath"/> and downloads each one.
    /// Empty lines and whitespace-only lines are skipped.
    /// </summary>
    public async Task<Result<BatchDownloadResult>> ExecuteAsync(
        string filePath,
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return Result<BatchDownloadResult>.Failure($"File not found: {filePath}");

        var urls = (await File.ReadAllLinesAsync(filePath, cancellationToken))
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (urls.Length == 0)
            return Result<BatchDownloadResult>.Failure("File contains no URLs");

        _logger.LogInformation("Starting batch download of {Count} URLs from {File}", urls.Length, filePath);

        int succeeded = 0, skipped = 0, failed = 0;

        for (int i = 0; i < urls.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _progress.Report(i + 1, urls.Length, $"Downloading {i + 1}/{urls.Length}");

            var result = await _singleDownload.ExecuteAsync(urls[i], options, cancellationToken);

            if (result.IsSuccess)
            {
                if (result.Value!.Type == DownloadResultType.Skipped) skipped++;
                else succeeded++;
            }
            else
            {
                failed++;
                _logger.LogWarning("Failed: {Url} — {Error}", urls[i], result.ErrorMessage);
            }
        }

        _progress.Log($"Batch complete: {succeeded} downloaded, {skipped} skipped, {failed} failed");
        return Result<BatchDownloadResult>.Success(new BatchDownloadResult(succeeded, skipped, failed, urls.Length));
    }
}

/// <summary>Summary statistics for a batch download operation.</summary>
public record BatchDownloadResult(int Succeeded, int Skipped, int Failed, int Total);
