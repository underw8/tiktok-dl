using System.Text.RegularExpressions;
using TikTokDl.Core.Domain.Common;

namespace TikTokDl.Core.Domain.Models;

/// <summary>
/// Value object representing a validated TikTok URL
/// </summary>
public sealed record TikTokUrl
{
    public string Value { get; }
    public TikTokUrlType Type { get; }
    public string? Username { get; }
    public string? VideoId { get; }
    
    private TikTokUrl(string value, TikTokUrlType type, string? username = null, string? videoId = null)
    {
        Value = value;
        Type = type;
        Username = username;
        VideoId = videoId;
    }
    
    /// <summary>
    /// Creates a TikTok URL from a string, validating its format
    /// </summary>
    public static Result<TikTokUrl> Create(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Result<TikTokUrl>.Failure("URL cannot be empty");
            
        url = url.Trim();
        
        // Try to parse as different TikTok URL types
        var videoResult = TryParseVideoUrl(url);
        if (videoResult.IsSuccess)
            return videoResult;
            
        var profileResult = TryParseProfileUrl(url);
        if (profileResult.IsSuccess)
            return profileResult;
            
        var shortResult = TryParseShortUrl(url);
        if (shortResult.IsSuccess)
            return shortResult;
            
        return Result<TikTokUrl>.Failure("Not a valid TikTok URL");
    }
    
    private static Result<TikTokUrl> TryParseVideoUrl(string url)
    {
        // Pattern: https://www.tiktok.com/@username/video/1234567890123456789
        var pattern = @"^https?://(www\.)?tiktok\.com/@([^/]+)/(video|photo)/(\d+)";
        var match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
        
        if (!match.Success)
            return Result<TikTokUrl>.Failure("Not a video URL");
            
        var username = match.Groups[2].Value;
        var videoId = match.Groups[4].Value;
        var type = match.Groups[3].Value.ToLower() == "photo" 
            ? TikTokUrlType.PhotoCarousel 
            : TikTokUrlType.Video;
            
        return Result<TikTokUrl>.Success(new TikTokUrl(url, type, username, videoId));
    }
    
    private static Result<TikTokUrl> TryParseProfileUrl(string url)
    {
        // Pattern: https://www.tiktok.com/@username
        var pattern = @"^https?://(www\.)?tiktok\.com/@([^/?]+)";
        var match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
        
        if (!match.Success)
            return Result<TikTokUrl>.Failure("Not a profile URL");
            
        var username = match.Groups[2].Value;
        return Result<TikTokUrl>.Success(new TikTokUrl(url, TikTokUrlType.Profile, username));
    }
    
    private static Result<TikTokUrl> TryParseShortUrl(string url)
    {
        // Pattern: https://vm.tiktok.com/ZMhhvQqg9/ or https://vt.tiktok.com/ZSjPrpCor/
        var pattern = @"^https?://(vm|vt)\.tiktok\.com/([A-Za-z0-9]+)/?";
        var match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
        
        if (!match.Success)
            return Result<TikTokUrl>.Failure("Not a short URL");
            
        return Result<TikTokUrl>.Success(new TikTokUrl(url, TikTokUrlType.Short));
    }
    
    public bool IsVideo => Type == TikTokUrlType.Video;
    public bool IsPhotoCarousel => Type == TikTokUrlType.PhotoCarousel;
    public bool IsProfile => Type == TikTokUrlType.Profile;
    public bool IsShort => Type == TikTokUrlType.Short;
    public bool RequiresExpansion => IsShort;
    
    public override string ToString() => Value;
}
