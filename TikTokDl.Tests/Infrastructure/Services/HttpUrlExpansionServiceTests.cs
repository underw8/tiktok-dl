using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TikTokDl.Core.Domain.Models;
using Xunit;
using TikTokDl.Core.Infrastructure.Services;

namespace TikTokDl.Tests.Infrastructure.Services;

public class HttpUrlExpansionServiceTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<HttpUrlExpansionService>> _mockLogger;
    private readonly HttpUrlExpansionService _sut;

    public HttpUrlExpansionServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockLogger = new Mock<ILogger<HttpUrlExpansionService>>();
        _sut = new HttpUrlExpansionService(_httpClient, _mockLogger.Object);
    }

    [Fact]
    public void CanExpand_ShortUrl_ReturnsTrue()
    {
        // Arrange
        var shortUrl = TikTokUrl.Create("https://vm.tiktok.com/ZMhhvQqg9/").Value!;

        // Act
        var result = _sut.CanExpand(shortUrl);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://www.tiktok.com/@testuser/video/123")]
    [InlineData("https://www.tiktok.com/@testuser")]
    public void CanExpand_NonShortUrl_ReturnsFalse(string url)
    {
        // Arrange
        var tikTokUrl = TikTokUrl.Create(url).Value!;

        // Act
        var result = _sut.CanExpand(tikTokUrl);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExpandUrlAsync_NonShortUrl_ReturnsFailure()
    {
        // Arrange
        var videoUrl = TikTokUrl.Create("https://www.tiktok.com/@testuser/video/123").Value!;

        // Act
        var result = await _sut.ExpandUrlAsync(videoUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("URL does not require expansion");
    }

    [Fact]
    public async Task ExpandUrlAsync_SuccessfulRedirect_ReturnsExpandedUrl()
    {
        // Arrange
        var shortUrl = TikTokUrl.Create("https://vm.tiktok.com/ZMhhvQqg9/").Value!;
        var expandedUrlString = "https://www.tiktok.com/@testuser/video/1234567890123456789";

        var response = new HttpResponseMessage(System.Net.HttpStatusCode.Found);
        response.Headers.Location = new Uri(expandedUrlString);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Head && 
                    req.RequestUri!.ToString() == shortUrl.Value),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _sut.ExpandUrlAsync(shortUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Value.Should().Be(expandedUrlString);
        result.Value.Type.Should().Be(TikTokUrlType.Video);
        result.Value.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task ExpandUrlAsync_RedirectToInvalidTikTokUrl_ReturnsFailure()
    {
        // Arrange
        var shortUrl = TikTokUrl.Create("https://vm.tiktok.com/ZMhhvQqg9/").Value!;
        var invalidExpandedUrl = "https://www.youtube.com/watch?v=123";

        var response = new HttpResponseMessage(System.Net.HttpStatusCode.Found);
        response.Headers.Location = new Uri(invalidExpandedUrl);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _sut.ExpandUrlAsync(shortUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("Expanded URL is not a valid TikTok URL");
    }

    [Fact]
    public async Task ExpandUrlAsync_NoRedirectFound_ReturnsFailure()
    {
        // Arrange
        var shortUrl = TikTokUrl.Create("https://vm.tiktok.com/ZMhhvQqg9/").Value!;

        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        // No Location header

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _sut.ExpandUrlAsync(shortUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("Unable to expand URL - no redirect found");
    }

    [Fact]
    public async Task ExpandUrlAsync_HttpRequestException_ReturnsNetworkError()
    {
        // Arrange
        var shortUrl = TikTokUrl.Create("https://vm.tiktok.com/ZMhhvQqg9/").Value!;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network is unreachable"));

        // Act
        var result = await _sut.ExpandUrlAsync(shortUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Network error");
    }

    [Fact]
    public async Task ExpandUrlAsync_Timeout_ReturnsTimeoutError()
    {
        // Arrange
        var shortUrl = TikTokUrl.Create("https://vm.tiktok.com/ZMhhvQqg9/").Value!;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out", new TimeoutException()));

        // Act
        var result = await _sut.ExpandUrlAsync(shortUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("Request timed out");
    }

    [Fact]
    public async Task ExpandUrlAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var shortUrl = TikTokUrl.Create("https://vm.tiktok.com/ZMhhvQqg9/").Value!;
        var cancellationTokenSource = new CancellationTokenSource();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage req, CancellationToken token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            });

        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ExpandUrlAsync(shortUrl, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ExpandUrlAsync_SetsCorrectUserAgent()
    {
        // Arrange
        var shortUrl = TikTokUrl.Create("https://vm.tiktok.com/ZMhhvQqg9/").Value!;
        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, token) =>
            {
                capturedRequest = req;
            })
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        // Act
        await _sut.ExpandUrlAsync(shortUrl);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.UserAgent.ToString().Should().Contain("Mozilla/5.0");
        capturedRequest.Method.Should().Be(HttpMethod.Head);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
