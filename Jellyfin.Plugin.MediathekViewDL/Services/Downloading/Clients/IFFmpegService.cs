using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediathekViewDL.Configuration.SubscriptionSettings;

namespace Jellyfin.Plugin.MediathekViewDL.Services.Downloading.Clients;

/// <summary>
/// Interface for the FFmpegService.
/// </summary>
public interface IFFmpegService
{
    /// <summary>
    /// Extracts the audio track from a url and saves it in the requested container format.
    /// The source audio codec is always copied without re-encoding.
    /// </summary>
    /// <param name="videoUrl">The path to the temporary input video file.</param>
    /// <param name="outputAudioPath">The path for the output audio file.</param>
    /// <param name="languageCode">The 3-letter language code (e.g., 'eng') to set in the metadata.</param>
    /// <param name="setOriginalLanguageTag">Whether to tag the audio as original language.</param>
    /// <param name="isAudioDescription">Whether the audio track is an audio description.</param>
    /// <param name="containerFormat">The output container format (Matroska Audio or MPEG-4 Audio).</param>
    /// <param name="progress">The progress reporter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="metadata">Optional metadata that will be embedded in the output container under the key <c>MediathekViewDL</c>.</param>
    /// <returns>True if the extraction was successful, otherwise false.</returns>
    Task<bool> ExtractAudioFromWebAsync(string videoUrl, string outputAudioPath, string languageCode, bool setOriginalLanguageTag, bool isAudioDescription, AudioContainerFormat containerFormat, IProgress<double> progress, CancellationToken cancellationToken, Metadata.MediaMetadata? metadata = null);

    /// <summary>
    /// Gets the media information (width, height, duration) from a remote URL or local file.
    /// </summary>
    /// <param name="urlOrPath">The URL or file path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The media info, or null if it could not be determined.</returns>
    Task<Library.LocalMediaInfo?> GetMediaInfoAsync(string urlOrPath, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a media file from a URL via FFmpeg and saves it as a local file.
    /// Supports both regular HTTP URLs and M3U8/HLS streams.
    /// </summary>
    /// <param name="url">The URL of the media file.</param>
    /// <param name="outputPath">The path for the output file.</param>
    /// <param name="readRate">The FFmpeg readrate multiplier. 0 means unlimited.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="metadata">Optional metadata that will be embedded in the output container under the key <c>MediathekViewDL</c>.</param>
    /// <returns>True if the download was successful, otherwise false.</returns>
    Task<bool> DownloadFileAsync(string url, string outputPath, int readRate, IProgress<double> progress, CancellationToken cancellationToken, Metadata.MediaMetadata? metadata = null);

    /// <summary>
    /// Executes FFmpeg with the specified arguments.
    /// </summary>
    /// <param name="args">The arguments to pass to FFmpeg.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="useProbe">If true, uses the ProbePath (ffprobe) instead of EncoderPath (ffmpeg).</param>
    /// <param name="progress">The progress reporter.</param>
    /// <returns>A tuple containing the exit code, standard output, and standard error.</returns>
    Task<(int ExitCode, string Output, string Error)> ExecuteFFmpegAsync(IEnumerable<string> args, CancellationToken cancellationToken, bool useProbe = false, IProgress<double>? progress = null);
}
