using System.IO;
using System.Xml.Serialization;
using Jellyfin.Plugin.MediathekViewDL.Configuration;
using Jellyfin.Plugin.MediathekViewDL.Configuration.SubscriptionSettings;
using Xunit;

namespace Jellyfin.Plugin.MediathekViewDL.Tests;

/// <summary>
/// Verifies that the new <see cref="SubscriptionSettings.AudioContainerFormat"/> setting defaults
/// correctly to <see cref="AudioContainerFormat.Mka"/> when deserializing a subscription that predates
/// this field (i.e. an XML fragment with no &lt;AudioContainerFormat&gt; element), matching how
/// Jellyfin server persists plugin configuration via its XML serializer.
/// </summary>
public class AudioContainerFormatSerializationTests
{
    [Fact]
    public void DownloadSettings_ShouldDefaultToMka_WhenXmlHasNoAudioContainerFormatElement()
    {
        // Arrange: an XML fragment representing a DownloadSettings saved before this field existed.
        const string legacyXml = """
            <DownloadSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <UseStreamingUrlFiles>false</UseStreamingUrlFiles>
              <DownloadFullVideoForSecondaryAudio>false</DownloadFullVideoForSecondaryAudio>
              <AllowFallbackToLowerQuality>true</AllowFallbackToLowerQuality>
              <QualityCheckWithUrl>false</QualityCheckWithUrl>
              <AlwaysCreateSubfolder>false</AlwaysCreateSubfolder>
              <EnhancedDuplicateDetection>false</EnhancedDuplicateDetection>
              <DownloadPath></DownloadPath>
            </DownloadSettings>
            """;

        var serializer = new XmlSerializer(typeof(DownloadSettings));

        // Act
        using var reader = new StringReader(legacyXml);
        var result = (DownloadSettings?)serializer.Deserialize(reader);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AudioContainerFormat.Mka, result.AudioContainerFormat);
    }

    [Fact]
    public void DownloadSettings_ShouldRoundTrip_WhenAudioContainerFormatIsExplicitlyMka()
    {
        // Arrange
        var original = new DownloadSettings { AudioContainerFormat = AudioContainerFormat.Mka };
        var serializer = new XmlSerializer(typeof(DownloadSettings));

        using var stringWriter = new StringWriter();
        serializer.Serialize(stringWriter, original);
        var xml = stringWriter.ToString();

        // Act
        using var reader = new StringReader(xml);
        var result = (DownloadSettings?)serializer.Deserialize(reader);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AudioContainerFormat.Mka, result.AudioContainerFormat);
    }

    [Fact]
    public void DownloadSettings_ShouldRoundTrip_WhenAudioContainerFormatIsExplicitlyM4a()
    {
        // Arrange
        var original = new DownloadSettings { AudioContainerFormat = AudioContainerFormat.M4a };
        var serializer = new XmlSerializer(typeof(DownloadSettings));

        using var stringWriter = new StringWriter();
        serializer.Serialize(stringWriter, original);
        var xml = stringWriter.ToString();

        // Act
        using var reader = new StringReader(xml);
        var result = (DownloadSettings?)serializer.Deserialize(reader);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AudioContainerFormat.M4a, result.AudioContainerFormat);
    }

    [Fact]
    public void BaseDownloadSettings_DefaultConstructor_ShouldHaveMkaAsDefault()
    {
        // Act
        var settings = new BaseDownloadSettings();

        // Assert
        Assert.Equal(AudioContainerFormat.Mka, settings.AudioContainerFormat);
    }
}
