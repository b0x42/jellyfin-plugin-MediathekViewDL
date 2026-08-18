## 1. Configuration

- [x] 1.1 Add `AudioContainerFormat` enum (`Mka`, `M4a`) to the `Configuration` (or appropriate shared) namespace, with an XML summary comment on the enum and on each value (per project convention of XML summary comments on public members).
- [x] 1.2 Add `AudioContainerFormat` property (default `M4a`) to `BaseDownloadSettings`, with an XML summary comment documenting the default and its rationale (podcast/external-player compatibility).
- [x] 1.3 Verify the property is available on `DownloadSettings` (per-subscription) via inheritance from `BaseDownloadSettings`, and defaults to `M4a` for a newly constructed `Subscription`.
- [ ] 1.4 Optionally wire `AudioContainerFormat` into `SubscriptionDefaults.DownloadSettings` purely as a Vue.js form pre-fill value (see task 6.1/6.3) - this has no runtime effect on the download pipeline; do not add server-side resolution logic that reads `SubscriptionDefaults` at download time, since no such mechanism exists for any other setting today.
- [ ] 1.5 Confirm the new enum property round-trips correctly through the plugin's `configuration.xml` persistence (`IXmlSerializer`/.NET `XmlSerializer`) - verify a `Subscription`/`PluginConfiguration` XML fixture saved before this change (with no `<AudioContainerFormat>` element present) deserializes with the property defaulting to `M4a` via the record's property initializer.

## 2. FFmpeg Service

- [ ] 2.1 Determine whether `IFFmpegService.ExtractAudioAsync` (the local-file variant) has any current caller in the codebase. If it is dead code, remove it as part of this change instead of updating it; if it is called, identify the caller and include it in scope for the format parameter change below. Record the finding so design.md's hedge ("if it remains in use") is resolved before implementation.
- [ ] 2.2 Update `IFFmpegService.ExtractAudioFromWebAsync` signature to accept the target `AudioContainerFormat` (and optional `MediaMetadata`), updating XML doc comments.
- [ ] 2.3 Based on 2.1's finding, either update `IFFmpegService.ExtractAudioAsync` signature similarly for consistency, or remove it (and its implementation/tests) if unused.
- [ ] 2.4 In `FFmpegService`, map `AudioContainerFormat.Mka` -> `-f matroska` and `AudioContainerFormat.M4a` -> `-f mp4`, keeping `-acodec copy` unchanged for both.
- [ ] 2.5 When format is `M4a` and metadata is provided, add `-movflags +use_metadata_tags` before the freeform `-metadata MediathekViewDL=<json>` argument; when format is `Mka`, embed metadata via `-metadata MediathekViewDL=<json>` as `DownloadFileAsync` already does for video.
- [ ] 2.6 Preserve existing language (`-metadata:s:a:0 language=...`) and disposition (`-disposition:a:0 ...`) argument construction unchanged for both formats.

## 3. Audio Extraction Handler

- [ ] 3.1 In `AudioExtractionHandler.ExecuteAsync`, read the subscription's `Download.AudioContainerFormat` (via `job`/whatever reference the handler has to the owning subscription) before building the temp file path. No fallback-resolution step is needed - read the field directly.
- [ ] 3.2 Update `TempFileHelper.GetTempFilePath` call to use the correct temp extension (`.mka` or `.m4a`) based on the format read in 3.1.
- [ ] 3.3 Pass the format and `job.MediaMetadata` through to `_ffmpegService.ExtractAudioFromWebAsync`.

## 4. File Naming

- [ ] 4.1 Update `FileNameBuilderService.BuildFileName` so the `FileType.Audio` case selects `.mka` or `.m4a` based on `subscription.Download.AudioContainerFormat` directly (no fallback resolution).
- [ ] 4.2 Confirm `GenerateDownloadPaths`/`DownloadPaths.MainFilePath` end-to-end produce the correct extension for both settings values.
- [ ] 4.3 Confirm `AudioContainerFormat` has no effect when `GetTargetMainType` resolves to `FileType.Strm` (i.e. `UseStreamingUrlFiles` is enabled) - no ffmpeg extraction happens in that path, so the setting is only relevant when the target type is `FileType.Audio`. Add a code comment noting this if not already obvious from control flow.

## 5. Library Scanning

- [ ] 5.1 Add `.m4a` to `LocalMediaScanner._videoExtensions` (or the appropriate recognized-audio-extensions list).

## 6. Vue.js Configuration UI

- [ ] 6.1 Add a format selector (e.g. `<select>` with `Mka`/`M4a` options) to `SettingsTab.vue` for `SubscriptionDefaults.DownloadSettings.AudioContainerFormat` - this only pre-fills new-subscription forms client-side (per Decision 1/task 1.4), consistent with how `UseStreamingUrlFiles` behaves there today.
- [ ] 6.2 Add the same selector to `SubscriptionEditor.vue` for the actual per-subscription `Download.AudioContainerFormat` value - this is the field that has real runtime effect.
- [ ] 6.3 Wire the new field into `PluginConfig.vue`'s defaults-apply/pre-fill logic (mirroring how `UseStreamingUrlFiles` is copied into `cfg.SubscriptionDefaults.DownloadSettings` and applied when initializing a new subscription's form state).
- [ ] 6.4 Add descriptive helper text near the control explaining the tradeoff (Matroska vs. MP4/podcast compatibility), matching the style of existing `field-desc` text.
- [ ] 6.5 Run `npm run build` in the VueJS directory to confirm the frontend still builds.

## 7. Tests

- [ ] 7.1 Update existing `FileNameBuilderServiceTests` default-path assertions (e.g. `GenerateDownloadPaths_ShouldAppendLanguage_WhenNotGerman`) to expect `.m4a` for the default configuration; add a new explicit-override test asserting `.mka` is produced when `AudioContainerFormat: Mka` is set.
- [ ] 7.2 Add `FFmpegService` tests (or equivalent) verifying muxer argument selection (`-f matroska` vs `-f mp4`) and the `-movflags +use_metadata_tags` argument only appearing for `.m4a` with metadata present.
- [ ] 7.3 Add `AudioExtractionHandler` tests verifying the subscription's format value drives both the temp file extension and the arguments passed to `IFFmpegService`, covering both the `M4a` default and an explicit `Mka` value.
- [ ] 7.4 Add a `LocalMediaScanner` test confirming `.m4a` files are recognized.
- [ ] 7.5 Search the test suite for any other hardcoded `.mka` default-path expectations and update them consistently.
- [ ] 7.6 Add a config serialization/deserialization test confirming a `PluginConfiguration`/`Subscription` XML fixture without an `<AudioContainerFormat>` element deserializes to `M4a` (covers task 1.5).
- [ ] 7.7 Run `dotnet test Jellyfin.Plugin.MediathekViewDL.sln` and confirm all tests pass.

## 8. Build

- [ ] 8.1 Run `dotnet build Jellyfin.Plugin.MediathekViewDL.sln` and confirm it succeeds with `TreatWarningsAsErrors=true`.

## 9. Documentation

- [ ] 9.1 Update `README.md` (German) to document the new `AudioContainerFormat` setting under the subscription/download settings section, alongside the existing `.strm` and metadata-embedding documentation: explain both `.m4a` and `.mka` options, state that `.m4a` is the default (changed from the previous hardcoded `.mka`), and explain how to set `.mka` per subscription to restore prior behavior.
- [ ] 9.2 Update the README's Table of Contents / feature table entries if the new setting warrants a mention there (matching how `.strm` support is already called out in the feature table).
- [ ] 9.3 Cross-check that the Vue.js helper text added in task 6.4 and the README wording are consistent (same tradeoff explanation - Matroska vs. MP4/podcast compatibility - so users see the same guidance in both places).
- [ ] 9.4 Update XML summary doc comments on any modified public API surface not already covered by tasks 1.1/1.2/2.2 (e.g. `AudioExtractionHandler`, `FileNameBuilderService` if their public doc comments describe the old hardcoded `.mka` behavior).
