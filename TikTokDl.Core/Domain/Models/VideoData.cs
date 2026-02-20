namespace TikTokDl.Core.Domain.Models;

/// <summary>
/// Represents resolved media data from the TikTok API
/// </summary>
public class VideoData
{
    /// <summary>Video download URL (empty for image carousels)</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Image URLs for photo carousel posts</summary>
    public List<string> Images { get; set; } = [];

    /// <summary>TikTok media ID (19-digit numeric string)</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Profile avatar image URLs</summary>
    public List<string> AvatarUrls { get; set; } = [];

    /// <summary>Profile GIF avatar URLs</summary>
    public List<string> GifAvatarUrls { get; set; } = [];

    /// <summary>Username of the content creator</summary>
    public string Name { get; set; } = string.Empty;

    public bool IsImageCarousel => Images.Count > 0;
    public bool IsVideo => !string.IsNullOrEmpty(Url) && Images.Count == 0;
}
