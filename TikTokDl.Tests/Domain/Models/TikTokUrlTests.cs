using FluentAssertions;
using TikTokDl.Core.Domain.Models;
using Xunit;

namespace TikTokDl.Tests.Domain.Models;

public class TikTokUrlTests
{
    [Theory]
    [InlineData("https://www.tiktok.com/@testuser/video/1234567890123456789")]
    [InlineData("http://www.tiktok.com/@testuser/video/1234567890123456789")]
    [InlineData("https://tiktok.com/@testuser/video/1234567890123456789")]
    public void Create_ValidVideoUrl_ReturnsSuccessWithVideoType(string url)
    {
        // Act
        var result = TikTokUrl.Create(url);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Type.Should().Be(TikTokUrlType.Video);
        result.Value.Username.Should().Be("testuser");
        result.Value.VideoId.Should().Be("1234567890123456789");
        result.Value.IsVideo.Should().BeTrue();
        result.Value.RequiresExpansion.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://www.tiktok.com/@testuser/photo/1234567890123456789")]
    [InlineData("http://www.tiktok.com/@testuser/photo/1234567890123456789")]
    public void Create_ValidPhotoUrl_ReturnsSuccessWithPhotoType(string url)
    {
        // Act
        var result = TikTokUrl.Create(url);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Type.Should().Be(TikTokUrlType.PhotoCarousel);
        result.Value.Username.Should().Be("testuser");
        result.Value.VideoId.Should().Be("1234567890123456789");
        result.Value.IsPhotoCarousel.Should().BeTrue();
        result.Value.RequiresExpansion.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://www.tiktok.com/@testuser")]
    [InlineData("http://www.tiktok.com/@testuser")]
    [InlineData("https://tiktok.com/@testuser")]
    public void Create_ValidProfileUrl_ReturnsSuccessWithProfileType(string url)
    {
        // Act
        var result = TikTokUrl.Create(url);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Type.Should().Be(TikTokUrlType.Profile);
        result.Value.Username.Should().Be("testuser");
        result.Value.VideoId.Should().BeNull();
        result.Value.IsProfile.Should().BeTrue();
        result.Value.RequiresExpansion.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://vm.tiktok.com/ZMhhvQqg9/")]
    [InlineData("https://vt.tiktok.com/ZSjPrpCor/")]
    [InlineData("http://vm.tiktok.com/ABC123")]
    public void Create_ValidShortUrl_ReturnsSuccessWithShortType(string url)
    {
        // Act
        var result = TikTokUrl.Create(url);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Type.Should().Be(TikTokUrlType.Short);
        result.Value.Username.Should().BeNull();
        result.Value.VideoId.Should().BeNull();
        result.Value.IsShort.Should().BeTrue();
        result.Value.RequiresExpansion.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyOrNullUrl_ReturnsFailure(string? url)
    {
        // Act
        var result = TikTokUrl.Create(url!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("URL cannot be empty");
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=123")]
    [InlineData("https://www.instagram.com/p/abc123")]
    [InlineData("https://www.facebook.com/video")]
    [InlineData("not-a-url")]
    [InlineData("https://www.tiktok.com/")]
    [InlineData("https://www.tiktok.com/invalid")]
    public void Create_InvalidUrl_ReturnsFailure(string url)
    {
        // Act
        var result = TikTokUrl.Create(url);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("Not a valid TikTok URL");
    }

    [Fact]
    public void Create_UrlWithSpecialCharactersInUsername_ReturnsSuccess()
    {
        // Arrange
        var url = "https://www.tiktok.com/@user.name_123/video/1234567890123456789";

        // Act
        var result = TikTokUrl.Create(url);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Username.Should().Be("user.name_123");
    }

    [Fact]
    public void ToString_ReturnsOriginalUrl()
    {
        // Arrange
        var originalUrl = "https://www.tiktok.com/@testuser/video/1234567890123456789";
        var result = TikTokUrl.Create(originalUrl);

        // Act
        var stringValue = result.Value!.ToString();

        // Assert
        stringValue.Should().Be(originalUrl);
    }

    [Fact]
    public void Value_ReturnsOriginalUrl()
    {
        // Arrange
        var originalUrl = "https://www.tiktok.com/@testuser/video/1234567890123456789";
        var result = TikTokUrl.Create(originalUrl);

        // Act
        var value = result.Value!.Value;

        // Assert
        value.Should().Be(originalUrl);
    }

    [Theory]
    [InlineData("https://www.tiktok.com/@testuser/video/123", true)]
    [InlineData("https://www.tiktok.com/@testuser/photo/123", true)]
    [InlineData("https://www.tiktok.com/@testuser", false)]
    [InlineData("https://vm.tiktok.com/abc123", false)]
    public void IsDownloadable_ReturnsCorrectValue(string url, bool expectedDownloadable)
    {
        // Arrange
        var tikTokUrl = TikTokUrl.Create(url).Value!;

        // Act
        var isDownloadable = tikTokUrl.IsVideo || tikTokUrl.IsPhotoCarousel;

        // Assert
        isDownloadable.Should().Be(expectedDownloadable);
    }
}
