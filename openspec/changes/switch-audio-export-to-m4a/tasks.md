## 1. Configuration

- [ ] 1.1 Add `AudioContainerFormat` enum (`Mka`, `M4a`) to the `Configuration` (or appropriate shared) namespace, with an XML summary comment on the enum and on each value (per project convention of XML summary comments on public members).
- [ ] 1.2 Add `AudioContainerFormat` property (default `M4a`) to `BaseDownloadSettings`, with an XML summary comment documenting the default and its rationale (podcast/external-player compatibility).
- [ ] 1.3 Verify the property is available and defaults correctly on both `DownloadSettings` (per-subscription) and wherever `SubscriptionDefaults` resolves download defaults.
- [ ] 1.4 Add/confirm config resolution logic so a subscription without an explicit override falls back to the global default value, consistent with how sibling `BaseDownloadSettings` fields resolve today.
- [ ] 1.5 Confirm the new enum property round-trips correctly through the plugin's configuration persistence (`System.Text.Json` serialization of `PluginConfiguration`, consistent with the project's "System.Text.Json only" rule) - verify existing configs without the field deserialize to the `M4a` default rather than failing or defaulting to the enum's zero-value if that differs.

## 2. FFmpeg Service

- [ ] 2.1 Determine whether `IFFmpegService.ExtractAudioAsync` (the local-file variant) has any current caller in the codebase. If it is dead code, remove it as part of this change instead of updating it; if it is called, identify the caller and include it in scope for the format parameter change below. Record the finding so design.md's hedge ("if it remains in use") is resolved before implementation.
- [ ] 2.2 Update `IFFmpegService.ExtractAudioFromWebAsync` signature to accept the target `AudioContainerFormat` (and optional `MediaMetadata`), updating XML doc comments.
- [ ] 2.3 Based on 2.1's finding, either update `IFFmpegService.ExtractAudioAsync` signature similarly for consistency, or remove it (and its implementation/tests) if unused.
- [ ] 2.4 In `FFmpegService`, map `AudioContainerFormat.Mka` -> `-f matroska` and `AudioContainerFormat.M4a` -> `-f mp4`, keeping `-acodec copy` unchanged for both.
- [ ] 2.5 When format is `M4a` and metadata is provided, add `-movflags +use_metadata_tags` before the freeform `-metadata MediathekViewDL=<json>` argument; when format is `Mka`, embed metadata via `-metadata MediathekViewDL=<json>` as `DownloadFileAsync` already does for video.
- [ ] 2.6 Preserve existing language (`-metadata:s:a:0 language=...`) and disposition (`-disposition:a:0 ...`) argument construction unchanged for both formats.

## 3. Audio Extraction Handler

- [ ] 3.1 In `AudioExtractionHandler.ExecuteAsync`, resolve the effective `AudioContainerFormat` (subscription override falling back to global default) before building the temp file path.
- [ ] 3.2 Update `TempFileHelper.GetTempFilePath` call to use the correct temp extension (`.mka` or `.m4a`) based on the resolved format.
- [ ] 3.3 Pass the resolved format and `job.MediaMetadata` through to `_ffmpegService.ExtractAudioFromWebAsync`.

## 4. File Naming

- [ ] 4.1 Update `FileNameBuilderService.BuildFileName` so the `FileType.Audio` case selects `.mka` or `.m4a` based on the effective `AudioContainerFormat` for the given subscription (resolving subscription override vs. global default the same way as task 1.4).
- [ ] 4.2 Confirm `GenerateDownloadPaths`/`DownloadPaths.MainFilePath` end-to-end produce the correct extension for both settings values.
- [ ] 4.3 Confirm `AudioContainerFormat` has no effect when `GetTargetMainType` resolves to `FileType.Strm` (i.e. `UseStreamingUrlFiles` is enabled) - no ffmpeg extraction happens in that path, so the setting is only relevant when the target type is `FileType.Audio`. Add a code comment noting this if not already obvious from control flow.

## 5. Library Scanning

- [ ] 5.1 Add `.m4a` to `LocalMediaScanner._videoExtensions` (or the appropriate recognized-audio-extensions list).

## 6. Vue.js Configuration UI

- [ ] 6.1 Add a format selector (e.g. `<select>` with `Mka`/`M4a` options) to `SettingsTab.vue` for the global `SubscriptionDefaults.DownloadSettings.AudioContainerFormat` default.
- [ ] 6.2 Add the same selector to `SubscriptionEditor.vue` for the per-subscription `Download.AudioContainerFormat` override.
- [ ] 6.3 Wire the new field into `PluginConfig.vue`'s defaults-apply logic (mirroring how `UseStreamingUrlFiles` is copied into `cfg.SubscriptionDefaults.DownloadSettings`).
- [ ] 6.4 Add descriptive helper text near the control explaining the tradeoff (Matroska vs. MP4/podcast compatibility), matching the style of existing `field-desc` text.
- [ ] 6.5 Run `npm run build` in the VueJS directory to confirm the frontend still builds.

## 7. Tests

- [ ] 7.1 Update existing `FileNameBuilderServiceTests` default-path assertions (e.g. `GenerateDownloadPaths_ShouldAppendLanguage_WhenNotGerman`) to expect `.m4a` for the default configuration; add a new explicit-override test asserting `.mka` is produced when `AudioContainerFormat: Mka` is set.
- [ ] 7.2 Add `FFmpegService` tests (or equivalent) verifying muxer argument selection (`-f matroska` vs `-f mp4`) and the `-movflags +use_metadata_tags` argument only appearing for `.m4a` with metadata present.
- [ ] 7.3 Add `AudioExtractionHandler` tests verifying the resolved format drives both the temp file extension and the arguments passed to `IFFmpegService`, covering both the `M4a` default and an explicit `Mka` override.
- [ ] 7.4 Add a `LocalMediaScanner` test confirming `.m4a` files are recognized.
- [ ] 7.5 Search the test suite for any other hardcoded `.mka` default-path expectations and update them consistently.
- [ ] 7.6 Add a config serialization/deserialization test confirming a `PluginConfiguration` without an explicit `AudioContainerFormat` value deserializes to `M4a` (covers task 1.5).
- [ ] 7.7 Run `dotnet test Jellyfin.Plugin.MediathekViewDL.sln` and confirm all tests pass.

## 8. Build & Docs

- [ ] 8.1 Run `dotnet build Jellyfin.Plugin.MediathekViewDL.sln` and confirm it succeeds with `TreatWarningsAsErrors=true`.
- [ ] 8.2 Update `README.md` to document the new audio container format setting, explicitly noting the default is now `.m4a` (changed from the previous hardcoded `.mka`) and how to restore `.mka` as default.
