using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediathekViewDL.Configuration.SubscriptionSettings;
using Jellyfin.Plugin.MediathekViewDL.Services.Downloading.Clients;
using Jellyfin.Plugin.MediathekViewDL.Services.Library;
using Jellyfin.Plugin.MediathekViewDL.Services.Metadata;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.MediathekViewDL.Tests;

/// <summary>
/// Tests for <see cref="FFmpegService.ExtractAudioFromWebAsync"/> argument construction.
/// These tests use a shell-script stub in place of the real ffmpeg binary to capture and assert on
/// the constructed command-line arguments (muxer selection, metadata embedding) without requiring
/// ffmpeg to be installed. The stub relies on a POSIX shell and is skipped on Windows.
/// </summary>
public class FFmpegServiceTests : IDisposable
{
    private readonly Mock<ILogger<FFmpegService>> _loggerMock;
    private readonly Mock<IMediaEncoder> _mediaEncoderMock;
    private readonly Mock<IStrmValidationService> _strmValidationServiceMock;
    private readonly FFmpegService _service;
    private readonly string _stubScriptPath;
    private readonly string _capturedArgsPath;
    private readonly bool _isSupportedPlatform;

    public FFmpegServiceTests()
    {
        _loggerMock = new Mock<ILogger<FFmpegService>>();
        _mediaEncoderMock = new Mock<IMediaEncoder>();
        _strmValidationServiceMock = new Mock<IStrmValidationService>();
        _isSupportedPlatform = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        _strmValidationServiceMock
            .Setup(s => s.ValidateUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _capturedArgsPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_args_{Guid.NewGuid():N}.txt");
        _stubScriptPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_stub_{Guid.NewGuid():N}.sh");

        if (_isSupportedPlatform)
        {
            // A stub "ffmpeg" that records the arguments it was invoked with, then exits successfully.
            File.WriteAllText(_stubScriptPath, $"#!/bin/sh\nprintf '%s\\n' \"$@\" > \"{_capturedArgsPath}\"\nexit 0\n");
#pragma warning disable CA1416 // Guarded by the _isSupportedPlatform (non-Windows) check above.
            File.SetUnixFileMode(_stubScriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
        }

        _mediaEncoderMock.SetupGet(m => m.EncoderPath).Returns(_stubScriptPath);

        _service = new FFmpegService(_loggerMock.Object, _mediaEncoderMock.Object, _strmValidationServiceMock.Object);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_stubScriptPath);
        }
        catch (IOException)
        {
        }

        try
        {
            File.Delete(_capturedArgsPath);
        }
        catch (IOException)
        {
        }
    }

    private string[] ReadCapturedArgs()
    {
        return File.Exists(_capturedArgsPath)
            ? File.ReadAllLines(_capturedArgsPath)
            : Array.Empty<string>();
    }

    [Fact]
    public async Task ExtractAudioFromWebAsync_ShouldUseMatroskaMuxer_WhenFormatIsMka()
    {
        if (!_isSupportedPlatform)
        {
            return;
        }

        // Act
        var result = await _service.ExtractAudioFromWebAsync(
            "https://example.com/video.mp4",
            "/tmp/out.mka",
            "eng",
            setOriginalLanguageTag: false,
            isAudioDescription: false,
            AudioContainerFormat.Mka,
            Mock.Of<IProgress<double>>(),
            CancellationToken.None);

        // Assert
        Assert.True(result);
        var args = ReadCapturedArgs();
        Assert.Contains("matroska", args);
        Assert.DoesNotContain("mp4", args);
        Assert.DoesNotContain("+use_metadata_tags", args);
    }

    [Fact]
    public async Task ExtractAudioFromWebAsync_ShouldUseMp4Muxer_WhenFormatIsM4a()
    {
        if (!_isSupportedPlatform)
        {
            return;
        }

        // Act
        var result = await _service.ExtractAudioFromWebAsync(
            "https://example.com/video.mp4",
            "/tmp/out.m4a",
            "eng",
            setOriginalLanguageTag: false,
            isAudioDescription: false,
            AudioContainerFormat.M4a,
            Mock.Of<IProgress<double>>(),
            CancellationToken.None);

        // Assert
        Assert.True(result);
        var args = ReadCapturedArgs();
        Assert.Contains("mp4", args);
        Assert.DoesNotContain("matroska", args);
    }

    [Fact]
    public async Task ExtractAudioFromWebAsync_ShouldAddUseMetadataTagsFlag_WhenFormatIsM4aAndMetadataProvided()
    {
        if (!_isSupportedPlatform)
        {
            return;
        }

        var metadata = new MediaMetadata { Id = "abc123", OriginalTitle = "Test" };

        // Act
        var result = await _service.ExtractAudioFromWebAsync(
            "https://example.com/video.mp4",
            "/tmp/out.m4a",
            "eng",
            setOriginalLanguageTag: false,
            isAudioDescription: false,
            AudioContainerFormat.M4a,
            Mock.Of<IProgress<double>>(),
            CancellationToken.None,
            metadata);

        // Assert
        Assert.True(result);
        var args = ReadCapturedArgs();
        Assert.Contains("+use_metadata_tags", args);
        Assert.Contains("-movflags", args);
        Assert.Contains(args, a => a.StartsWith("MediathekViewDL=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExtractAudioFromWebAsync_ShouldNotAddUseMetadataTagsFlag_WhenFormatIsMkaAndMetadataProvided()
    {
        if (!_isSupportedPlatform)
        {
            return;
        }

        var metadata = new MediaMetadata { Id = "abc123", OriginalTitle = "Test" };

        // Act
        var result = await _service.ExtractAudioFromWebAsync(
            "https://example.com/video.mp4",
            "/tmp/out.mka",
            "eng",
            setOriginalLanguageTag: false,
            isAudioDescription: false,
            AudioContainerFormat.Mka,
            Mock.Of<IProgress<double>>(),
            CancellationToken.None,
            metadata);

        // Assert
        Assert.True(result);
        var args = ReadCapturedArgs();
        Assert.DoesNotContain("+use_metadata_tags", args);
        Assert.Contains(args, a => a.StartsWith("MediathekViewDL=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExtractAudioFromWebAsync_ShouldPreserveLanguageAndDispositionArguments()
    {
        if (!_isSupportedPlatform)
        {
            return;
        }

        // Act
        var result = await _service.ExtractAudioFromWebAsync(
            "https://example.com/video.mp4",
            "/tmp/out.m4a",
            "eng",
            setOriginalLanguageTag: true,
            isAudioDescription: true,
            AudioContainerFormat.M4a,
            Mock.Of<IProgress<double>>(),
            CancellationToken.None);

        // Assert
        Assert.True(result);
        var args = ReadCapturedArgs();
        Assert.Contains("language=eng", args);
        Assert.Contains("original+visual_impaired", args);
        Assert.Contains("-acodec", args);
        Assert.Contains("copy", args);
    }
}

