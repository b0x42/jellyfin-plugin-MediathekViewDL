## Why

Audio-only exports (secondary-language audio extraction) currently always produce `.mka` (Matroska Audio) files. `.mka` is well supported inside Jellyfin but poorly supported by external podcast/audio clients (Apple Podcasts, Overcast, Pocket Casts, mobile media players), which expect `.m4a`/MP4-family containers. Since the extraction already uses `-acodec copy` (no re-encoding, source is AAC), switching the container to MP4/`.m4a` is a lossless, drop-in muxer change. Making it configurable (rather than a hard switch) avoids disrupting existing users/libraries that rely on `.mka`.

## What Changes

- Add a new per-subscription/global setting, `AudioContainerFormat` (enum: `Mka`, `M4a`; default `M4a` for better compatibility with external podcast/audio players), to `BaseDownloadSettings` (so it is available at both the global `SubscriptionDefaults` and per-subscription `DownloadSettings` levels, consistent with other download settings). **BREAKING**: newly downloaded audio-only files default to `.m4a` instead of `.mka`; existing `.mka` files are unaffected (no migration), but users relying on the previous default extension for external tooling/automation must explicitly set `Mka` to keep prior behavior.
- `FileNameBuilderService.BuildFileName` selects `.mka` or `.m4a` as the file extension for `FileType.Audio` based on the effective setting.
- `FFmpegService.ExtractAudioFromWebAsync` (and `ExtractAudioAsync`) select the FFmpeg muxer (`-f matroska` vs `-f mp4`) based on the requested container, still using `-acodec copy` in both cases (no transcoding).
- Preserve the existing language (`-metadata:s:a:0 language=...`) and disposition (`-disposition:a:0 original+visual_impaired`) tags for both containers.
- Add embedding of the existing `MediaMetadata` JSON blob (currently only embedded for full video/`.strm` downloads, not for audio extraction) into the audio output for both `.mka` and `.m4a`, using the container-appropriate metadata mechanism (Matroska generic tag vs. MP4 freeform `-metadata` key), so parity is maintained between formats and with the existing video/`.strm` metadata behavior.
- `LocalMediaScanner`'s recognized audio extensions gain `.m4a` alongside the existing `.mka`.
- No migration of already-downloaded `.mka` files; the setting only affects newly created downloads going forward.

## Capabilities

### New Capabilities

- `audio-extraction`: Audio-only downloads must support a configurable output container (`.mka` or `.m4a`), select the corresponding FFmpeg muxer while keeping codec passthrough (`-acodec copy`), and embed the plugin's `MediaMetadata` JSON payload into the output file regardless of which container is selected.

## Impact

- **Config**: `BaseDownloadSettings` (new `AudioContainerFormat` property), affects `DownloadSettings` and `SubscriptionDefaults`; Vue.js configuration UI needs a new control to select the format (global default + per-subscription override, matching existing setting patterns).
- **Code**: `FileNameBuilderService` (extension selection), `IFFmpegService`/`FFmpegService` (muxer selection + metadata embedding for audio extraction), `AudioExtractionHandler` (pass through `job.MediaMetadata` and effective container format), `LocalMediaScanner` (recognize `.m4a`).
- **Tests**: Existing `.mka`-focused default-path tests in `FileNameBuilderServiceTests` need their expected extension updated to `.m4a` (the new default); a new explicit-override test covers the previous `.mka` behavior. New tests also needed for `.m4a`/`.mka` muxer selection and for metadata embedding on both container paths.
- **Docs**: `README.md` needs a note about the new setting.
- **BREAKING (minor)**: the default output extension for new audio-only downloads changes from `.mka` to `.m4a`. No existing files are touched or migrated; only downloads performed after upgrading are affected. Users who need to keep `.mka` as the default can set `AudioContainerFormat: Mka` globally.
