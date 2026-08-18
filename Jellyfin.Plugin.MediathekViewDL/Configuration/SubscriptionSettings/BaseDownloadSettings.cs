namespace Jellyfin.Plugin.MediathekViewDL.Configuration.SubscriptionSettings;

/// <summary>
/// Base settings for the download process.
/// </summary>
public record BaseDownloadSettings
{
    /// <summary>
    /// Gets a value indicating whether to use streaming URL files (.strm) instead of downloading the actual video files.
    /// </summary>
    public bool UseStreamingUrlFiles { get; init; }

    /// <summary>
    /// Gets a value indicating whether to download the full video for secondary audio languages.
    /// If false, only the audio track will be extracted for secondary languages.
    /// </summary>
    public bool DownloadFullVideoForSecondaryAudio { get; init; }

    /// <summary>
    /// Gets a value indicating whether to allow falling back to lower quality versions
    /// if HD version is not available.
    /// </summary>
    public bool AllowFallbackToLowerQuality { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to check if the URL retrieved from MediathekViewWeb API is valid.
    /// If not it will try with the next lower quality available.
    /// This can slow down the Scan. Especially if thers a lot of unavailable videos.
    /// </summary>
    public bool QualityCheckWithUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether to always create a subfolder for the subscription (using the subscription name),
    /// even if the content is a movie and the global setting 'UseTopicForMoviePath' is disabled.
    /// </summary>
    public bool AlwaysCreateSubfolder { get; init; }

    /// <summary>
    /// Gets a value indicating whether to enable enhanced duplicate detection.
    /// If enabled, the target directory is scanned for existing files matching the season/episode pattern.
    /// </summary>
    public bool EnhancedDuplicateDetection { get; init; }

    /// <summary>
    /// Gets the output container format used for audio-only downloads (secondary-language audio
    /// extraction without video). Defaults to <see cref="AudioContainerFormat.M4a"/> for compatibility
    /// with external podcast/audio player applications. The source audio is always copied without
    /// re-encoding regardless of the selected container. Changing this setting does not affect
    /// files that were already downloaded; it only applies to downloads performed after the change.
    /// </summary>
    public AudioContainerFormat AudioContainerFormat { get; init; } = AudioContainerFormat.M4a;
}
