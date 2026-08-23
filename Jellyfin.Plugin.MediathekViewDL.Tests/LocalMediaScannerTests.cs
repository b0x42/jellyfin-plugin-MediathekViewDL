using System;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.MediathekViewDL.Services.Library;
using Jellyfin.Plugin.MediathekViewDL.Services.Media;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.MediathekViewDL.Tests;

public class LocalMediaScannerTests : IDisposable
{
    private readonly Mock<ILogger<LocalMediaScanner>> _loggerMock;
    private readonly Mock<IVideoParser> _videoParserMock;
    private readonly LocalMediaScanner _scanner;
    private readonly string _tempDir;

    public LocalMediaScannerTests()
    {
        _loggerMock = new Mock<ILogger<LocalMediaScanner>>();
        _videoParserMock = new Mock<IVideoParser>();
        _scanner = new LocalMediaScanner(_loggerMock.Object, _videoParserMock.Object);

        _tempDir = Path.Combine(Path.GetTempPath(), $"lms_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void ScanSubscriptionDirectory_ShouldRecognizeM4aFiles_AsVideoType()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "Some Show - S01E01.m4a");
        File.WriteAllText(filePath, "stub");

        _videoParserMock
            .Setup(p => p.ParseVideoInfo(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new VideoInfo { Title = "Some Show", SeasonNumber = 1, EpisodeNumber = 1 });

        // Act
        var result = _scanner.ScanSubscriptionDirectory(_tempDir, "Some Show");

        // Assert
        var scannedFile = result.Files.FirstOrDefault(f => f.FilePath == filePath);
        Assert.NotNull(scannedFile);
        Assert.Equal(FileType.Video, scannedFile.Type);
    }

    [Fact]
    public void ScanSubscriptionDirectory_ShouldStillRecognizeMkaFiles_AsVideoType()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "Some Show - S01E02.mka");
        File.WriteAllText(filePath, "stub");

        _videoParserMock
            .Setup(p => p.ParseVideoInfo(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new VideoInfo { Title = "Some Show", SeasonNumber = 1, EpisodeNumber = 2 });

        // Act
        var result = _scanner.ScanSubscriptionDirectory(_tempDir, "Some Show");

        // Assert
        var scannedFile = result.Files.FirstOrDefault(f => f.FilePath == filePath);
        Assert.NotNull(scannedFile);
        Assert.Equal(FileType.Video, scannedFile.Type);
    }
}
