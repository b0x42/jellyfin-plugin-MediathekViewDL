## Context

See proposal.md - Why. Relevant current state:

- `FFmpegService.ExtractAudioFromWebAsync`/`ExtractAudioAsync` hardcode `-f matroska` and `-acodec copy`; there is no format parameter today.
- `FileNameBuilderService.BuildFileName` hardcodes `.mka` for `FileType.Audio` with no configuration input.
- `AudioExtractionHandler.ExecuteAsync` does not pass `job.MediaMetadata` to the FFmpeg call at all today - metadata embedding for audio-only exports does not currently exist (only `.mkv`/`.strm` downloads embed it, via `FFmpegDownloadHandler`/`StreamingUrlHandler`).
- Config settings that affect the download pipeline follow a two-level pattern: `BaseDownloadSettings` (shared record) is used both by `DownloadSettings` (per-subscription, via `Subscription.Download`) and implicitly by `SubscriptionDefaults` (global defaults applied when a subscription doesn't override). The Vue.js UI (`SettingsTab.vue` for defaults, `SubscriptionEditor.vue` for per-subscription overrides) mirrors these fields 1:1.

## Goals / Non-Goals

**Goals:**
- Add a container format choice (`Mka` | `M4a`) usable at both the global-default and per-subscription level, following the existing settings pattern exactly.
- Default new installs and upgrades to `M4a` for out-of-the-box podcast/external-player compatibility, while leaving already-downloaded files untouched.
- Keep `-acodec copy` (no transcoding) for both formats.
- Embed the existing `MediaMetadata` JSON payload into audio-only outputs for both formats (a new capability for the audio-extraction path, matching what already happens for `.mkv`/`.strm`).
- Allow users who need the previous `.mka` default to restore it via a single global setting.

**Non-Goals:**
- Adding more container formats (e.g. `.opus`, `.mp3`) - those would require actual transcoding and are out of scope.
- Migrating or converting already-downloaded `.mka` files.
- Changing the video (`.mkv`) or `.strm` download paths - this only affects the `FileType.Audio` / `DownloadType.AudioExtraction` path.

## Decisions

### 1. New enum `AudioContainerFormat { Mka, M4a }` on `BaseDownloadSettings`

Placing it on `BaseDownloadSettings` (rather than a new standalone settings group) reuses the existing inheritance already shared by `DownloadSettings` (per-subscription) and the defaults consumed through `SubscriptionDefaults.DownloadSettings`. This matches how `DownloadFullVideoForSecondaryAudio` and other download-shape toggles are already modeled, so resolution order (subscription override -> global default) requires no new plumbing beyond what config-resolution code already does for sibling settings.

Alternative considered: a separate top-level `AudioOptions` group (parallel to `DownloadOptions`). Rejected because the setting is subscription-scoped by requirement (per user's confirmed choice of "configurable" implying per-subscription control), and `BaseDownloadSettings` is exactly the record designed for that.

Default value: `M4a`, for compatibility with external podcast/audio players out of the box. This is a deliberate behavior change for new downloads: existing installs that don't set the value explicitly will see newly downloaded audio-only files switch from `.mka` to `.m4a` after upgrading. No previously downloaded `.mka` files are touched or migrated (per the "no migration" goal) - only the default for *future* downloads changes. Users who need to keep `.mka` as their default must explicitly set `AudioContainerFormat: Mka` globally (or per subscription).

### 2. FFmpeg muxer selection: `-f matroska` (`.mka`) vs `-f mp4` (`.m4a`)

Both keep `-acodec copy`. `IFFmpegService.ExtractAudioFromWebAsync` gains a format parameter (enum, not a raw string) that the implementation maps to the muxer name and to the metadata-embedding strategy (see Decision 3). `AudioExtractionHandler` resolves the effective format (subscription override, falling back to global default) before calling the service, the same way other resolved settings are read in that handler's siblings (e.g. `FFmpegDownloadHandler` reading `_configProvider.Configuration.Download.ReadRate`).

`IFFmpegService.ExtractAudioAsync` (the local-file-input variant) is a separate method from `ExtractAudioFromWebAsync` (the URL-input variant actually used by `AudioExtractionHandler`). Whether it needs the same treatment depends on whether it currently has a live caller - this is a factual question to resolve during implementation (task 2.1), not a design ambiguity: if unused, remove it rather than carry dead code through the format change; if used, apply the same format parameter for consistency.

Alternative considered: keep `-f mp4` unconditionally and only vary the extension. Rejected - `.m4a` files must be muxed with the MP4 container (`-f mp4`; ffmpeg does not have a distinct "m4a" muxer name, `mp4` is used for both `.mp4` and `.m4a` outputs), so the muxer argument must switch, not just the file extension.

### 3. Metadata embedding parity across containers

Matroska already supports arbitrary freeform `-metadata key=value` tags that round-trip via generic Matroska tag storage - this is what `DownloadFileAsync` already relies on for `.mkv`. MP4/`.m4a`, however, only writes the fixed set of iTunes-style tags by default; arbitrary key/value pairs are dropped unless `-movflags +use_metadata_tags` is passed to the muxer ([Super User: "custom tags can be written if -movflags use_metadata_tags is added"](https://superuser.com/questions/1208273/how-to-add-new-and-non-defined-metadata-to-an-mp4-file/1208277)). So:
- For `.mka` output: pass `-metadata MediathekViewDL=<json>` exactly as `DownloadFileAsync` does today.
- For `.m4a` output: additionally pass `-movflags +use_metadata_tags` so the same freeform `-metadata MediathekViewDL=<json>` key survives muxing.

This keeps the embedded payload and its key name (`MediathekViewDlDbContext`/`MediaMetadataKeys.MetadataKey`) identical across both formats, so any downstream code reading it (if added later) does not need per-container logic.

Alternative considered: map `MediaMetadata` fields onto native MP4 iTunes atoms (`©nam` for title, etc.) instead of one JSON blob. Rejected for this change - it would fragment the single-source-of-truth JSON payload used elsewhere (`.mkv`, `.strm`) into two different serialization strategies, increasing maintenance cost for a benefit not required by the proposal (the goal is metadata preservation/parity, not native tag mapping for external players).

### 4. `AudioExtractionHandler` gains metadata embedding it didn't have before

Since `ExecuteAsync` currently never forwards `job.MediaMetadata`, this change adds that forwarding for both container formats as part of implementing Decision 3 - not a behavior regression to preserve, but a new capability applied uniformly to close the gap identified while investigating the existing code.

### 5. `LocalMediaScanner` recognizes `.m4a`

Add `.m4a` to the existing `_videoExtensions` array (already named generically enough to include `.mka`) so files aren't skipped during library scans.

### 6. `AudioContainerFormat` does not apply to the `.strm` output path

`FileNameBuilderService.GetTargetMainType` can resolve to `FileType.Strm` when `UseStreamingUrlFiles` is enabled (or for non-episode extras when configured). In that path, no ffmpeg extraction happens at all - a `.strm` file is written pointing at the streaming URL directly (`StreamingUrlHandler`/`FileDownloader.GenerateStreamingUrlFileAsync`), so there is no audio container to choose. `AudioContainerFormat` is therefore only consulted when the resolved type is `FileType.Audio`; it has no effect on `.strm` output and no effect on `.mkv` (`FileType.Video`) output either (that path already embeds metadata via `DownloadFileAsync`'s existing `-f matroska` muxer choice, unrelated to this setting). This is a clarification of existing control flow, not new branching logic - `GetTargetMainType`/`BuildFileName` already select one `FileType` per download and the new setting only affects the `FileType.Audio` case.

### 7. Config persistence of the new enum

`PluginConfiguration` is persisted by Jellyfin server's plugin configuration store using `System.Text.Json` (per project convention: "`System.Text.Json` only"). A new enum property added to `BaseDownloadSettings` will serialize as its string name by default with `System.Text.Json`'s default enum handling (or as an integer if no `JsonStringEnumConverter` is configured project-wide) - the implementation must confirm which convention this project already uses for other enums (if any exist) and match it for consistency, and confirm that a config file saved before this change (with no `AudioContainerFormat` property present) deserializes with the property defaulting to `M4a` rather than to the enum's implicit zero value, which would silently produce the opposite of the intended default if `Mka` were declared first in the enum. This is verified by task 1.5/7.6, not left as an assumption.

## Risks / Trade-offs

- [Risk] MP4 muxer with `-movflags +use_metadata_tags` may behave inconsistently across FFmpeg versions bundled with different Jellyfin server installs. -> Mitigation: verify behavior against the FFmpeg version pinned/expected by Jellyfin server compatibility during implementation; if freeform tags are unsupported on an old FFmpeg build, the extraction still succeeds (audio + language/disposition tags), only the JSON payload embedding silently no-ops the same way partial FFmpeg failures are already logged elsewhere.
- [Risk] Vue.js config UI needs a new enum-style control (dropdown/radio) where existing sibling settings are all booleans (checkboxes) - slightly different UI pattern. -> Mitigation: use a simple `<select>` bound to the string enum value, consistent with any other existing non-boolean settings pattern in the same forms (e.g. numeric input fields), keeping the change additive and small.
- [Risk] Existing tests assert `.mka` unconditionally for the default configuration (e.g. `FileNameBuilderServiceTests.GenerateDownloadPaths_ShouldAppendLanguage_WhenNotGerman`). -> Mitigation: since the default changes to `M4a`, these tests must be updated to expect `.m4a` for the default case; add a separate explicit-`Mka`-override test to cover the previous behavior so it isn't lost from the suite.

## Migration Plan

No data migration required (per user decision: existing `.mka` files are left as-is). Deployment is a standard plugin version bump:
1. Ship the new setting defaulted to `M4a`.
2. On upgrade, subscriptions/installs that don't explicitly set `AudioContainerFormat` start producing `.m4a` for new audio-only downloads; previously downloaded `.mka` files remain untouched on disk.
3. Users who want to keep `.mka` set `AudioContainerFormat: Mka` globally or per subscription.
4. Rollback: reverting the plugin version removes the setting and restores the old hardcoded `.mka` behavior; any subscriptions already producing `.m4a` files simply stop producing new ones after rollback, with no data loss to existing files either way.
5. Call out the default change prominently in the release notes/README, since it is a behavior change for users who upgrade without reading the changelog.
