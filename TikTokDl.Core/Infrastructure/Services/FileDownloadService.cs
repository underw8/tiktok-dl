using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Domain.Common;

namespace TikTokDl.Core.Infrastructure.Services;

/// <summary>
/// Streams files to disk with an 8192-byte buffer, resume support, and retry logic.
/// Ported from MainForm.cs:DownloadVideoWithBufferedWrite() (line 1993).
/// </summary>
public class FileDownloadService : IFileDownloadService
{
    private const int BufferSize = 8192;
    private const int MaxRetries = 5;
    private const int PreDownloadDelayMs = 1900;

    private readonly ILogger<FileDownloadService> _logger;

    public FileDownloadService(ILogger<FileDownloadService> logger)
    {
        _logger = logger;
    }

    public async Task<Result> DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int retryCount = MaxRetries;

        while (retryCount > 0)
        {
            try
            {
                long totalBytesRead = 0;

                // Resume if partial file exists; otherwise apply rate-limit delay
                if (File.Exists(destinationPath))
                {
                    totalBytesRead = new FileInfo(destinationPath).Length;
                }
                else
                {
                    await Task.Delay(PreDownloadDelayMs, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();

                using var client = new HttpClient();
                using var response = await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    retryCount--;
                    _logger.LogWarning("HTTP 429 downloading file, {RetriesLeft} retries left", retryCount);
                    if (retryCount == 0)
                        return Result.Failure("Download failed — HTTP 429 after max retries");
                    await Task.Delay(10_000, cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(
                    destinationPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    useAsync: true);

                var buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytesRead += bytesRead;
                    progress?.Report(totalBytesRead);
                }

                return Result.Success();
            }
            catch (TaskCanceledException)
            {
                return Result.Failure("Download cancelled");
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                retryCount--;
                _logger.LogWarning(ex, "Socket error, {RetriesLeft} retries left", retryCount);
                if (retryCount == 0)
                    return Result.Failure("Download failed after max retries (socket errors)");
            }
            catch (IOException ex)
            {
                retryCount--;
                _logger.LogWarning(ex, "IO error during download, {RetriesLeft} retries left", retryCount);
                if (retryCount == 0)
                    return Result.Failure($"Download failed after max retries: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                retryCount--;
                _logger.LogWarning(ex, "HTTP error during download, {RetriesLeft} retries left", retryCount);
                if (retryCount == 0)
                    return Result.Failure($"Download failed — HTTP error: {ex.Message}");
                await Task.Delay(5_000, cancellationToken);
            }
        }

        return Result.Failure("Download failed");
    }

    public async Task<Result> DownloadImageAsync(
        string url,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        int retryCount = MaxRetries;

        while (retryCount > 0)
        {
            try
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    retryCount--;
                    _logger.LogWarning("HTTP 429 downloading image, {RetriesLeft} retries left", retryCount);
                    if (retryCount == 0)
                        return Result.Failure("Image download failed — HTTP 429 after max retries");
                    await Task.Delay(10_000, cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = File.Create(destinationPath);
                await stream.CopyToAsync(fileStream, cancellationToken);
                return Result.Success();
            }
            catch (TaskCanceledException)
            {
                return Result.Failure("Download cancelled");
            }
            catch (HttpRequestException ex)
            {
                retryCount--;
                _logger.LogWarning(ex, "HTTP error downloading image, {RetriesLeft} retries left", retryCount);
                if (retryCount == 0)
                    return Result.Failure($"Image download failed: {ex.Message}");
                await Task.Delay(5_000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading image from {Url}", url);
                return Result.Failure($"Image download failed: {ex.Message}");
            }
        }

        return Result.Failure("Image download failed after max retries");
    }
}
