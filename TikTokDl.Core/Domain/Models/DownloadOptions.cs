namespace TikTokDl.Core.Domain.Models;

/// <summary>
/// Options controlling how a download is performed. Replaces WinForms checkbox/control state.
/// </summary>
public record DownloadOptions(
    bool Hd,
    bool WithWatermark,
    bool DownloadAvatar,
    bool EnableJsonLogs,
    bool EnableDownloadLogs,
    string OutputDirectory,
    string? CustomBrowserPath = null
)
{
    public static DownloadOptions Default => new(
        Hd: false,
        WithWatermark: false,
        DownloadAvatar: false,
        EnableJsonLogs: false,
        EnableDownloadLogs: true,
        OutputDirectory: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "TiktokDownloads"),
        CustomBrowserPath: null
    );
}
