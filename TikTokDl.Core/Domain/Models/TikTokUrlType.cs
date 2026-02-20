namespace TikTokDl.Core.Domain.Models;

/// <summary>
/// Represents the different types of TikTok URLs
/// </summary>
public enum TikTokUrlType
{
    /// <summary>
    /// A video post URL (e.g., https://www.tiktok.com/@user/video/123)
    /// </summary>
    Video,
    
    /// <summary>
    /// A photo carousel post URL (e.g., https://www.tiktok.com/@user/photo/123)
    /// </summary>
    PhotoCarousel,
    
    /// <summary>
    /// A user profile URL (e.g., https://www.tiktok.com/@user)
    /// </summary>
    Profile,
    
    /// <summary>
    /// A short URL that needs to be expanded (e.g., https://vm.tiktok.com/abc123)
    /// </summary>
    Short
}
