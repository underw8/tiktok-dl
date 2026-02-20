using TikTokDl.Core.Domain.Common;

namespace TikTokDl.Core.Application.Interfaces;

/// <summary>
/// Handles streaming file downloads with resume support and retry logic.
/// </summary>
public interface IFileDownloadService
{
    /// <summary>
    /// Downloads a file to disk, streaming with an 8192-byte buffer.
    /// Resumes partial downloads and retries on socket/IO errors (max 5 times).
    /// </summary>
    /// <param name="url">URL to download from</param>
    /// <param name="destinationPath">Full local file path</param>
    /// <param name="progress">Optional progress callback (bytesWritten)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a simple file (image) without resume support.
    /// </summary>
    Task<Result> DownloadImageAsync(
        string url,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
