using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediathekViewDL.Configuration;
using Jellyfin.Plugin.MediathekViewDL.Services.Metadata;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Jellyfin.Plugin.MediathekViewDL.Tests;

public class EpisodeArtworkServiceTests : IDisposable
{
    private const string WebsiteUrl = "https://www.zdf.de/video/talk/example-100";
    private const string ImageUrl = "https://www.zdf.de/assets/example-thumb~1920x1080";

    private readonly Mock<ILogger<EpisodeArtworkService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<IConfigurationProvider> _configProviderMock;
    private readonly EpisodeArtworkService _service;
    private readonly string _tempDir;

    public EpisodeArtworkServiceTests()
    {
        _loggerMock = new Mock<ILogger<EpisodeArtworkService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _configProviderMock = new Mock<IConfigurationProvider>();

        _configProviderMock.Setup(x => x.ConfigurationOrNull).Returns(new PluginConfiguration());

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _service = new EpisodeArtworkService(httpClient, _loggerMock.Object, _configProviderMock.Object);

        _tempDir = Path.Combine(Path.GetTempPath(), $"MediathekViewDL_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); }
            catch { /* cleanup best-effort */ }
        }
    }

    private void SetupResponses(string html, byte[]? imageBytes, HttpStatusCode imageStatus = HttpStatusCode.OK)
    {
        var pageResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) };
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == new Uri(WebsiteUrl)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(pageResponse);

        var imageResponse = new HttpResponseMessage(imageStatus)
        {
            Content = new ByteArrayContent(imageBytes ?? Array.Empty<byte>())
        };
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == new Uri(ImageUrl)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(imageResponse);
    }

    [Fact]
    public async Task DownloadArtworkAsync_SuccessfulOgImage_DownloadsAndSavesFile()
    {
        // Arrange
        var html = $"<html><head><meta property=\"og:image\" content=\"{ImageUrl}\" /></head></html>";
        var imageBytes = new byte[] { 1, 2, 3, 4 };
        SetupResponses(html, imageBytes);
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = WebsiteUrl };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(artworkPath));
        Assert.Equal(imageBytes, await File.ReadAllBytesAsync(artworkPath));
    }

    [Fact]
    public async Task DownloadArtworkAsync_OgImageWithReversedAttributeOrder_IsStillFound()
    {
        // Arrange -- content before property, as some pages emit it.
        var html = $"<html><head><meta content=\"{ImageUrl}\" property=\"og:image\" /></head></html>";
        var imageBytes = new byte[] { 5, 6, 7 };
        SetupResponses(html, imageBytes);
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = WebsiteUrl };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(imageBytes, await File.ReadAllBytesAsync(artworkPath));
    }

    [Fact]
    public async Task DownloadArtworkAsync_NoOgImageTag_ReturnsFalse()
    {
        // Arrange
        var html = "<html><head><title>No image here</title></head></html>";
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == new Uri(WebsiteUrl)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = WebsiteUrl };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.False(File.Exists(artworkPath));
    }

    [Fact]
    public async Task DownloadArtworkAsync_WebsitePageNotFound_ReturnsFalse()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == new Uri(WebsiteUrl)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = WebsiteUrl };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DownloadArtworkAsync_ImageDownloadFails_ReturnsFalse()
    {
        // Arrange
        var html = $"<html><head><meta property=\"og:image\" content=\"{ImageUrl}\" /></head></html>";
        SetupResponses(html, null, HttpStatusCode.InternalServerError);
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = WebsiteUrl };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.False(File.Exists(artworkPath));
    }

    [Fact]
    public async Task DownloadArtworkAsync_DisallowedWebsiteDomain_ReturnsFalseWithoutRequest()
    {
        // Arrange
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = "https://malicious-site.com/episode-100" };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.False(result);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DownloadArtworkAsync_DisallowedImageDomain_ReturnsFalse()
    {
        // Arrange -- the page itself is on an allowed domain, but its og:image points
        // somewhere disallowed.
        var html = "<html><head><meta property=\"og:image\" content=\"https://malicious-cdn.com/x.jpg\" /></head></html>";
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == new Uri(WebsiteUrl)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = WebsiteUrl };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.False(File.Exists(artworkPath));
    }

    [Fact]
    public async Task DownloadArtworkAsync_NullConfig_ReturnsFalse()
    {
        // Arrange
        _configProviderMock.Setup(x => x.ConfigurationOrNull).Returns((PluginConfiguration?)null);
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = WebsiteUrl };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DownloadArtworkAsync_EmptyWebsiteUrl_ReturnsFalse()
    {
        // Arrange
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = string.Empty };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DownloadArtworkAsync_HttpRequestException_ReturnsFalse()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var artworkPath = Path.Combine(_tempDir, "episode.jpg");
        var item = new EpisodeArtworkDTO { FilePath = artworkPath, WebsiteUrl = WebsiteUrl };

        // Act
        var result = await _service.DownloadArtworkAsync(item, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.False(File.Exists(artworkPath));
    }
}
