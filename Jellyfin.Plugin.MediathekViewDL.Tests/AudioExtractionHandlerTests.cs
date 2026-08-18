using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediathekViewDL.Configuration;
using Jellyfin.Plugin.MediathekViewDL.Configuration.SubscriptionSettings;
using Jellyfin.Plugin.MediathekViewDL.Services.Downloading.Clients;
using Jellyfin.Plugin.MediathekViewDL.Services.Downloading.Handlers;
using Jellyfin.Plugin.MediathekViewDL.Services.Downloading.Models;
using Jellyfin.Plugin.MediathekViewDL.Services.Media;
using Jellyfin.Plugin.MediathekViewDL.Services.Metadata;
using MediaBrowser.Controller;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.MediathekViewDL.Tests;

public class AudioExtractionHandlerTests
{
    private readonly Mock<ILogger<AudioExtractionHandler>> _loggerMock;
    private readonly Mock<IFFmpegService> _ffmpegServiceMock;
    private readonly Mock<IConfigurationProvider> _configProviderMock;
    private readonly Mock<IServerApplicationPaths> _appPathsMock;
    private readonly AudioExtractionHandler _handler;

    public AudioExtractionHandlerTests()
    {
        _loggerMock = new Mock<ILogger<AudioExtractionHandler>>();
        _ffmpegServiceMock = new Mock<IFFmpegService>();
        _configProviderMock = new Mock<IConfigurationProvider>();
        _appPathsMock = new Mock<IServerApplicationPaths>();

        _configProviderMock.Setup(x => x.ConfigurationOrNull).Returns(new PluginConfiguration());
        _appPathsMock.Setup(x => x.TempDirectory).Returns(System.IO.Path.GetTempPath());

        _handler = new AudioExtractionHandler(_loggerMock.Object, _ffmpegServiceMock.Object, _configProviderMock.Object, _appPathsMock.Object);
    }

    private static DownloadJob CreateJob(AudioContainerFormat format, MediaMetadata? metadata = null)
    {
        return new DownloadJob
        {
            ItemId = "test-item",
            Title = "Test Item",
            ItemInfo = new VideoInfo { Title = "Test Item", Language = "eng" },
            AudioContainerFormat = format,
            MediaMetadata = metadata,
        };
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForAudioExtraction()
    {
        // Act
        var result = _handler.CanHandle(DownloadType.AudioExtraction);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanHandle_ShouldReturnFalse_ForOtherTypes()
    {
        // Act
        var result = _handler.CanHandle(DownloadType.FFmpegDownload);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseM4aTempExtension_WhenJobFormatIsM4a()
    {
        // Arrange
        string? capturedTempPath = null;
        _ffmpegServiceMock
            .Setup(s => s.ExtractAudioFromWebAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<AudioContainerFormat>(),
                It.IsAny<IProgress<double>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<MediaMetadata?>()))
            .Returns<string, string, string, bool, bool, AudioContainerFormat, IProgress<double>, CancellationToken, MediaMetadata?>(
                (_, tempPath, _, _, _, _, _, _, _) =>
                {
                    capturedTempPath = tempPath;
                    System.IO.File.WriteAllText(tempPath, "stub-audio-content");
                    return Task.FromResult(true);
                });

        var job = CreateJob(AudioContainerFormat.M4a);
        var destPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.m4a");
        var item = new DownloadItem { SourceUrl = "https://example.com/video.mp4", DestinationPath = destPath, JobType = DownloadType.AudioExtraction };

        try
        {
            // Act
            var result = await _handler.ExecuteAsync(item, job, Mock.Of<IProgress<double>>(), CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.NotNull(capturedTempPath);
            Assert.EndsWith(".m4a.mvdl-tmp", capturedTempPath);
        }
        finally
        {
            if (System.IO.File.Exists(destPath))
            {
                System.IO.File.Delete(destPath);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseMkaTempExtension_WhenJobFormatIsMka()
    {
        // Arrange
        string? capturedTempPath = null;
        _ffmpegServiceMock
            .Setup(s => s.ExtractAudioFromWebAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<AudioContainerFormat>(),
                It.IsAny<IProgress<double>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<MediaMetadata?>()))
            .Returns<string, string, string, bool, bool, AudioContainerFormat, IProgress<double>, CancellationToken, MediaMetadata?>(
                (_, tempPath, _, _, _, _, _, _, _) =>
                {
                    capturedTempPath = tempPath;
                    System.IO.File.WriteAllText(tempPath, "stub-audio-content");
                    return Task.FromResult(true);
                });

        var job = CreateJob(AudioContainerFormat.Mka);
        var destPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.mka");
        var item = new DownloadItem { SourceUrl = "https://example.com/video.mp4", DestinationPath = destPath, JobType = DownloadType.AudioExtraction };

        try
        {
            // Act
            var result = await _handler.ExecuteAsync(item, job, Mock.Of<IProgress<double>>(), CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.NotNull(capturedTempPath);
            Assert.EndsWith(".mka.mvdl-tmp", capturedTempPath);
        }
        finally
        {
            if (System.IO.File.Exists(destPath))
            {
                System.IO.File.Delete(destPath);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassContainerFormatAndMetadata_ToFFmpegService()
    {
        // Arrange
        var metadata = new MediaMetadata { Id = "abc", OriginalTitle = "Test" };
        AudioContainerFormat? capturedFormat = null;
        MediaMetadata? capturedMetadata = null;

        _ffmpegServiceMock
            .Setup(s => s.ExtractAudioFromWebAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<AudioContainerFormat>(),
                It.IsAny<IProgress<double>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<MediaMetadata?>()))
            .Returns<string, string, string, bool, bool, AudioContainerFormat, IProgress<double>, CancellationToken, MediaMetadata?>(
                (_, tempPath, _, _, _, format, _, _, meta) =>
                {
                    capturedFormat = format;
                    capturedMetadata = meta;
                    System.IO.File.WriteAllText(tempPath, "stub-audio-content");
                    return Task.FromResult(true);
                });

        var job = CreateJob(AudioContainerFormat.M4a, metadata);
        var destPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.m4a");
        var item = new DownloadItem { SourceUrl = "https://example.com/video.mp4", DestinationPath = destPath, JobType = DownloadType.AudioExtraction };

        try
        {
            // Act
            await _handler.ExecuteAsync(item, job, Mock.Of<IProgress<double>>(), CancellationToken.None);

            // Assert
            Assert.Equal(AudioContainerFormat.M4a, capturedFormat);
            Assert.Same(metadata, capturedMetadata);
        }
        finally
        {
            if (System.IO.File.Exists(destPath))
            {
                System.IO.File.Delete(destPath);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetOriginalLanguageTagFalse_ForGermanContent_ViaDownloadAudioOnlyForPrimaryLanguage()
    {
        // Arrange: German content only reaches audio-only extraction via DownloadAudioOnlyForPrimaryLanguage.
        // setOriginalLanguageTag is derived from itemInfo.Language != "deu", so it must be false here -
        // German is the primary/default language, not an "original version" (OV) track.
        bool? capturedSetOriginalLanguageTag = null;

        _ffmpegServiceMock
            .Setup(s => s.ExtractAudioFromWebAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<AudioContainerFormat>(),
                It.IsAny<IProgress<double>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<MediaMetadata?>()))
            .Returns<string, string, string, bool, bool, AudioContainerFormat, IProgress<double>, CancellationToken, MediaMetadata?>(
                (_, tempPath, _, setOriginalLanguageTag, _, _, _, _, _) =>
                {
                    capturedSetOriginalLanguageTag = setOriginalLanguageTag;
                    System.IO.File.WriteAllText(tempPath, "stub-audio-content");
                    return Task.FromResult(true);
                });

        var job = new DownloadJob
        {
            ItemId = "test-item",
            Title = "German Show",
            ItemInfo = new VideoInfo { Title = "German Show", Language = "deu" },
            AudioContainerFormat = AudioContainerFormat.M4a,
        };
        var destPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.m4a");
        var item = new DownloadItem { SourceUrl = "https://example.com/video.mp4", DestinationPath = destPath, JobType = DownloadType.AudioExtraction };

        try
        {
            // Act
            var result = await _handler.ExecuteAsync(item, job, Mock.Of<IProgress<double>>(), CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.False(capturedSetOriginalLanguageTag);
        }
        finally
        {
            if (System.IO.File.Exists(destPath))
            {
                System.IO.File.Delete(destPath);
            }
        }
    }
}
