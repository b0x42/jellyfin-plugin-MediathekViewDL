namespace Jellyfin.Plugin.MediathekViewDL.Services.Metadata;

/// <summary>
/// Data Transfer Object describing an episode artwork file to be downloaded.
/// </summary>
public class EpisodeArtworkDTO
{
    /// <summary>
    /// Gets or sets the full file path where the artwork image will be saved.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL of the broadcaster's website page for the episode, from which
    /// the artwork (the page's <c>og:image</c> teaser image) is derived.
    /// </summary>
    public string WebsiteUrl { get; set; } = string.Empty;
}
