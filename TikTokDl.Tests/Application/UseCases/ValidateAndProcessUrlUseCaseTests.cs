using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Application.UseCases;
using Xunit;
using TikTokDl.Core.Domain.Common;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.Tests.Application.UseCases;

public class ValidateAndProcessUrlUseCaseTests
{
    private readonly Mock<IUrlExpansionService> _mockUrlExpansionService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<ValidateAndProcessUrlUseCase>> _mockLogger;
    private readonly ValidateAndProcessUrlUseCase _sut;

    public ValidateAndProcessUrlUseCaseTests()
    {
        _mockUrlExpansionService = new Mock<IUrlExpansionService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<ValidateAndProcessUrlUseCase>>();
        
        _sut = new ValidateAndProcessUrlUseCase(
            _mockUrlExpansionService.Object,
            _mockNotificationService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ValidVideoUrl_ReturnsSuccessResult()
    {
        // Arrange
        var videoUrl = "https://www.tiktok.com/@testuser/video/1234567890123456789";

        // Act
        var result = await _sut.ExecuteAsync(videoUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.OriginalUrl.Type.Should().Be(TikTokUrlType.Video);
        result.Value.ProcessedUrl.Type.Should().Be(TikTokUrlType.Video);
        result.Value.WasExpanded.Should().BeFalse();
        result.Value.IsDownloadable.Should().BeTrue();
        result.Value.IsBatchDownload.Should().BeFalse();

        // Verify notification was sent
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            "URL Validated",
            It.Is<string>(msg => msg.Contains("Ready to download video from @testuser")),
            NotificationType.Success), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ValidProfileUrl_ReturnsSuccessForBatchDownload()
    {
        // Arrange
        var profileUrl = "https://www.tiktok.com/@testuser";

        // Act
        var result = await _sut.ExecuteAsync(profileUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OriginalUrl.Type.Should().Be(TikTokUrlType.Profile);
        result.Value.ProcessedUrl.Type.Should().Be(TikTokUrlType.Profile);
        result.Value.WasExpanded.Should().BeFalse();
        result.Value.IsDownloadable.Should().BeFalse();
        result.Value.IsBatchDownload.Should().BeTrue();

        // Verify notification was sent
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            "URL Validated",
            It.Is<string>(msg => msg.Contains("Ready for batch download from @testuser")),
            NotificationType.Success), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ValidShortUrl_ExpandsAndReturnsSuccess()
    {
        // Arrange
        var shortUrl = "https://vm.tiktok.com/ZMhhvQqg9/";
        var expandedUrl = TikTokUrl.Create("https://www.tiktok.com/@testuser/video/1234567890123456789").Value!;
        
        _mockUrlExpansionService
            .Setup(x => x.ExpandUrlAsync(It.IsAny<TikTokUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TikTokUrl>.Success(expandedUrl));

        // Act
        var result = await _sut.ExecuteAsync(shortUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OriginalUrl.Type.Should().Be(TikTokUrlType.Short);
        result.Value.ProcessedUrl.Type.Should().Be(TikTokUrlType.Video);
        result.Value.WasExpanded.Should().BeTrue();
        result.Value.IsDownloadable.Should().BeTrue();

        // Verify expansion service was called
        _mockUrlExpansionService.Verify(x => x.ExpandUrlAsync(
            It.Is<TikTokUrl>(url => url.IsShort),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify success notification
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            "URL Validated",
            It.Is<string>(msg => msg.Contains("Ready to download video from @testuser")),
            NotificationType.Success), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidUrl_ReturnsFailure()
    {
        // Arrange
        var invalidUrl = "https://www.youtube.com/watch?v=123";

        // Act
        var result = await _sut.ExecuteAsync(invalidUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("Not a valid TikTok URL");

        // Verify warning notification was sent
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            "Invalid URL",
            "Not a valid TikTok URL",
            NotificationType.Warning), Times.Once);

        // Verify expansion service was not called
        _mockUrlExpansionService.Verify(x => x.ExpandUrlAsync(
            It.IsAny<TikTokUrl>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShortUrlExpansionFails_ReturnsFailure()
    {
        // Arrange
        var shortUrl = "https://vm.tiktok.com/ZMhhvQqg9/";
        var expansionError = "Network error";
        
        _mockUrlExpansionService
            .Setup(x => x.ExpandUrlAsync(It.IsAny<TikTokUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TikTokUrl>.Failure(expansionError));

        // Act
        var result = await _sut.ExecuteAsync(shortUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be(expansionError);

        // Verify error notification was sent
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            "URL Expansion Failed",
            expansionError,
            NotificationType.Error), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyUrl_ReturnsFailure()
    {
        // Arrange
        var emptyUrl = "";

        // Act
        var result = await _sut.ExecuteAsync(emptyUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("URL cannot be empty");

        // Verify warning notification was sent
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            "Invalid URL",
            "URL cannot be empty",
            NotificationType.Warning), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_PropagatesCancellation()
    {
        // Arrange
        var shortUrl = "https://vm.tiktok.com/ZMhhvQqg9/";
        var cancellationTokenSource = new CancellationTokenSource();
        
        _mockUrlExpansionService
            .Setup(x => x.ExpandUrlAsync(It.IsAny<TikTokUrl>(), It.IsAny<CancellationToken>()))
            .Returns((TikTokUrl url, CancellationToken token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(Result<TikTokUrl>.Success(url));
            });

        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ExecuteAsync(shortUrl, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedError_ReturnsFailureWithGenericMessage()
    {
        // Arrange
        var shortUrl = "https://vm.tiktok.com/ZMhhvQqg9/";
        
        _mockUrlExpansionService
            .Setup(x => x.ExpandUrlAsync(It.IsAny<TikTokUrl>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected service error"));

        // Act
        var result = await _sut.ExecuteAsync(shortUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Unexpected error");

        // Verify error notification was sent
        _mockNotificationService.Verify(x => x.ShowNotificationAsync(
            "Processing Error",
            "An unexpected error occurred while processing the URL",
            NotificationType.Error), Times.Once);
    }
}
