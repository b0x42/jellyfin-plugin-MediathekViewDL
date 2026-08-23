using System.Collections.Generic;

namespace Jellyfin.Plugin.MediathekViewDL.Services.Downloading.Models;

/// <summary>
/// Represents the result of a download job execution with per-item details.
/// </summary>
public sealed class DownloadJobResult
{
    /// <summary>
    /// Gets a value indicating whether the overall job was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the per-item results.
    /// </summary>
    public IReadOnlyList<DownloadItemResult> ItemResults { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether episode artwork was downloaded for this job, or
    /// <c>null</c> if the job had no <see cref="Downloading.Models.DownloadJob.ArtworkMetadata"/>
    /// to begin with. Unlike every other item in <see cref="ItemResults"/>, artwork is
    /// best-effort by design: a failed or skipped artwork fetch never fails the overall job
    /// (see <see cref="Downloading.DownloadManager"/>), so this flag is purely informational.
    /// </summary>
    public bool? ArtworkDownloaded { get; init; }
}
