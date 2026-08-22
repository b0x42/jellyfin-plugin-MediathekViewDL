using Jellyfin.Plugin.MediathekViewDL.Api.Converters;
using Jellyfin.Plugin.MediathekViewDL.Api.External.Models;
using Xunit;

namespace Jellyfin.Plugin.MediathekViewDL.Tests;

public class DtoConverterTests
{
    private static ResultItem BuildResultItem(string? urlWebsite = "https://www.zdf.de/video/talk/example-100")
    {
        return new ResultItem
        {
            Id = "id-123",
            Title = "Beispiel vom 1. Januar 2026",
            Topic = "Beispielsendung",
            Channel = "ZDF",
            Description = "Eine Beschreibung.",
            Timestamp = 1735689600,
            Duration = 3600,
            Size = 123456,
            UrlVideo = "https://example.com/video.mp4",
            UrlVideoLow = "https://example.com/video-low.mp4",
            UrlVideoHd = "https://example.com/video-hd.mp4",
            UrlSubtitle = "https://example.com/sub.vtt",
            UrlWebsite = urlWebsite!,
        };
    }

    [Fact]
    public void ToDto_ShouldMapWebsiteUrl_WhenPresent()
    {
        // Arrange
        var resultItem = BuildResultItem("https://www.zdf.de/video/talk/example-100");

        // Act
        var dto = resultItem.ToDto(upgradeToHttps: false);

        // Assert
        Assert.Equal("https://www.zdf.de/video/talk/example-100", dto.WebsiteUrl);
    }

    [Fact]
    public void ToDto_ShouldMapWebsiteUrlToNull_WhenEmpty()
    {
        // Arrange
        var resultItem = BuildResultItem(string.Empty);

        // Act
        var dto = resultItem.ToDto(upgradeToHttps: false);

        // Assert
        Assert.Null(dto.WebsiteUrl);
    }

    [Fact]
    public void ToDto_ShouldUpgradeWebsiteUrlToHttps_WhenRequested()
    {
        // Arrange
        var resultItem = BuildResultItem("http://www.zdf.de/video/talk/example-100");

        // Act
        var dto = resultItem.ToDto(upgradeToHttps: true);

        // Assert
        Assert.NotNull(dto.WebsiteUrl);
        Assert.StartsWith("https://", dto.WebsiteUrl);
        Assert.EndsWith("/video/talk/example-100", dto.WebsiteUrl);
    }

    [Fact]
    public void ToDto_ShouldNotUpgradeWebsiteUrl_WhenNotRequested()
    {
        // Arrange
        var resultItem = BuildResultItem("http://www.zdf.de/video/talk/example-100");

        // Act
        var dto = resultItem.ToDto(upgradeToHttps: false);

        // Assert
        Assert.Equal("http://www.zdf.de/video/talk/example-100", dto.WebsiteUrl);
    }
}
