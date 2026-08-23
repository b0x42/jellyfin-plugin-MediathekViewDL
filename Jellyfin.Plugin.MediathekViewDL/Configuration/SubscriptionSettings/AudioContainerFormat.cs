namespace Jellyfin.Plugin.MediathekViewDL.Configuration.SubscriptionSettings;

/// <summary>
/// Defines the output container format used when extracting audio-only downloads.
/// The source audio stream is always copied without re-encoding; this only selects the muxer.
/// </summary>
public enum AudioContainerFormat
{
    /// <summary>
    /// MPEG-4 Audio (.m4a). Well supported by external podcast and audio player applications.
    /// </summary>
    M4a,

    /// <summary>
    /// Matroska Audio (.mka). Well supported inside Jellyfin, but poorly supported by
    /// external podcast/audio clients.
    /// </summary>
    Mka
}
