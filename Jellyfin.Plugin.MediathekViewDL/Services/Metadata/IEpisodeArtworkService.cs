using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.MediathekViewDL.Services.Metadata;

/// <summary>
/// Service for downloading episode artwork derived from a broadcaster's website page.
/// </summary>
public interface IEpisodeArtworkService
{
    /// <summary>
    /// Fetches the given website page, extracts its <c>og:image</c> teaser image, and
    /// downloads that image to <see cref="EpisodeArtworkDTO.FilePath"/>.
    /// </summary>
    /// <param name="item">The artwork item describing the source page and destination path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if the artwork was downloaded successfully, otherwise <c>false</c>.
    /// Never throws for expected failure modes (network errors, missing og:image, disallowed
    /// domains); the artwork is best-effort and must not fail the overall download job.</returns>
    Task<bool> DownloadArtworkAsync(EpisodeArtworkDTO item, CancellationToken cancellationToken);
}
