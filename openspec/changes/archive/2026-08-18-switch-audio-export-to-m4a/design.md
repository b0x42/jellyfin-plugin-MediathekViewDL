## Context

See proposal.md - Why. Relevant current state:

- `FFmpegService.ExtractAudioFromWebAsync`/`ExtractAudioAsync` hardcode `-f matroska` and `-acodec copy`; there is no format parameter today.
- `FileNameBuilderService.BuildFileName` hardcodes `.mka` for `FileType.Audio` with no configuration input.
- `AudioExtractionHandler.ExecuteAsync` does not pass `job.MediaMetadata` to the FFmpeg call at all today - metadata embedding for audio-only exports does not currently exist (only `.mkv`/`.strm` downloads embed it, via `FFmpegDownloadHandler`/`StreamingUrlHandler`).
- Config settings that affect the download pipeline are plain properties on `BaseDownloadSettings` (shared record), used by `DownloadSettings` (per-subscription, via `Subscription.Download`). There is **no runtime "global default" resolution**: `SubscriptionDefaults.DownloadSettings` exists but its own doc comment says "Currently without function" - no C# code reads it at runtime. It is only consumed client-side by the Vue.js UI (`SetupWizard.vue`, `SettingsTab.vue`, `PluginConfig.vue`) to pre-fill the form shown when a user creates a new subscription; once a `Subscription` is saved, every consumer (`FileNameBuilderService`, `AudioExtractionHandler`, etc.) reads `subscription.Download.X` directly with no fallback beyond the C# property's own default value.

## Goals / Non-Goals

**Goals:**
- Add a container format choice (`Mka` | `M4a`) as a plain per-subscription setting on `BaseDownloadSettings`, matching how every other field on that record behaves today (a C#-level default, no runtime fallback resolution).
- Add an opt-in trigger, `DownloadAudioOnlyForPrimaryLanguage`, that lets a subscription request audio-only downloads for German (primary-language) content, using the existing audio-extraction pipeline unchanged.
- Default new subscriptions to `M4a` for out-of-the-box podcast/external-player compatibility, while leaving already-downloaded files untouched.
- Keep `-acodec copy` (no transcoding) for both formats.
- Embed the existing `MediaMetadata` JSON payload into audio-only outputs for both formats (a new capability for the audio-extraction path, matching what already happens for `.mkv`/`.strm`).
- Optionally let the Vue.js "subscription defaults" pre-fill mechanism (`SubscriptionDefaults.DownloadSettings`) include both new fields, consistent with how it already pre-fills `UseStreamingUrlFiles` - purely a UI convenience, not a runtime behavior.

**Non-Goals:**
- Adding more container formats (e.g. `.opus`, `.mp3`) - those would require actual transcoding and are out of scope.
- Migrating or converting already-downloaded `.mka` files.
- Changing the `.strm` download path - this and the `AudioContainerFormat` setting are unrelated (see Decision 6).

## Decisions

### 1. New enum `AudioContainerFormat { Mka, M4a }` on `BaseDownloadSettings`

Placing it on `BaseDownloadSettings` (rather than a new standalone settings group) reuses the existing inheritance already shared by `DownloadSettings` (per-subscription). This matches how `DownloadFullVideoForSecondaryAudio` and other download-shape toggles are already modeled: a plain property with a C# default value and no runtime resolution step.

Alternative considered: implement genuine "subscription override falls back to a live global default" resolution logic, wiring `SubscriptionDefaults.DownloadSettings.AudioContainerFormat` as a runtime fallback read at download time. Rejected after investigation - **no such resolution mechanism exists today for any `BaseDownloadSettings` field.** `SubscriptionDefaults` is documented as "Currently without function" and is only consumed by the Vue.js frontend to pre-fill new-subscription forms; introducing live fallback resolution just for this one field would be inconsistent with every sibling setting and a materially larger change than proposed. `AudioContainerFormat` therefore behaves exactly like `DownloadFullVideoForSecondaryAudio`: a static default, set once per subscription, with no separate "global" runtime value.

Default value: `M4a`, for compatibility with external podcast/audio players out of the box. This is a deliberate behavior change for new downloads: subscriptions created without setting this field explicitly will produce `.m4a` instead of the old hardcoded `.mka`. No previously downloaded `.mka` files are touched or migrated (per the "no migration" goal) - only the default for *newly created subscriptions and their future downloads* changes. Existing subscriptions created before this change was deployed will have their `AudioContainerFormat` deserialize to whatever the missing-field default resolves to (see Decision 7) - this must also resolve to `M4a` for consistency, since there is no persisted value to distinguish an "old" subscription from a "new" one otherwise. Users who need `.mka` set `AudioContainerFormat: Mka` explicitly on that subscription.

### 2. FFmpeg muxer selection: `-f matroska` (`.mka`) vs `-f mp4` (`.m4a`)

Both keep `-acodec copy`. `IFFmpegService.ExtractAudioFromWebAsync` gains a format parameter (enum, not a raw string) that the implementation maps to the muxer name and to the metadata-embedding strategy (see Decision 3). `AudioExtractionHandler` reads `job.AudioContainerFormat` directly (no resolution step, per Decision 1) before calling the service. That value is populated once, at job-creation time in `SubscriptionProcessor.GetJobsForSubscriptionAsync`, from `subscription.Download.AudioContainerFormat` - the same pattern already used for `job.MediaMetadata` (also precomputed once at job creation rather than re-derived by each handler). `DownloadJob.AudioContainerFormat` defaults to `AudioContainerFormat.M4a` for jobs that don't go through `SubscriptionProcessor` (i.e. manual downloads built by `DownloadsController`); this is safe because those code paths never produce `DownloadType.AudioExtraction` items (manual downloads always force `FileType.Video`), so the field is simply unused there.

Investigated during implementation: `IFFmpegService.ExtractAudioAsync` (the local-file-input variant) had **no caller anywhere in the codebase** (confirmed via full-repo search of both production and test code) - it was dead code and has been removed rather than updated, per the plan in this decision.

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

`FileNameBuilderService.GetTargetMainType` can resolve to `FileType.Strm` when `UseStreamingUrlFiles` is enabled (or for non-episode extras when configured). In that path, no ffmpeg extraction happens at all - a `.strm` file is written pointing at the streaming URL directly (`StreamingUrlHandler`/`FileDownloader.GenerateStreamingUrlFileAsync`), so there is no audio container to choose. `AudioContainerFormat` is therefore only consulted when the resolved type is `FileType.Audio`; it has no effect on `.strm` output. It also has no direct effect on `.mkv` (`FileType.Video`) output (that path already embeds metadata via `DownloadFileAsync`'s existing `-f matroska` muxing, unrelated to this setting) - but see Decision 8 for how `DownloadAudioOnlyForPrimaryLanguage` changes *which* content reaches the `FileType.Video` vs. `FileType.Audio` branch in the first place.

### 7. Config persistence of the new enum

`PluginConfiguration` is persisted by Jellyfin server as `configuration.xml` via `IXmlSerializer`/.NET `XmlSerializer` (`Plugin.cs` takes `IXmlSerializer` in its constructor) - not JSON. The concrete risk is the same one called out in Decision 1: a subscription record saved *before* this change exists on disk with no `<AudioContainerFormat>` XML element at all. `XmlSerializer` deserializes by first constructing the object via its parameterless constructor (running the record's property initializers, including `AudioContainerFormat { get; init; } = AudioContainerFormat.M4a`) and then only overwrites properties for elements actually present in the XML - so a missing element leaves the initializer's value (`M4a`) in place. This is standard `XmlSerializer` behavior and requires no additional handling beyond the property initializer already added; task 1.5/7.6 exists to verify this empirically (deserialize an XML fixture without the element) rather than rely on this reasoning alone.

### 8. `DownloadAudioOnlyForPrimaryLanguage` composes with existing `GetTargetMainType` branching, not alongside it

This setting was originally developed on a separate branch (`feat/audio-only-german-subscription`) targeting the same underlying user need - an audio-only download option - as `AudioContainerFormat`, and the two were merged into this change after the fact (see proposal.md - Why). The composition is a small, surgical edit to the existing condition in `GetTargetMainType`:

```
if ((videoInfo is { Language: "deu", HasAudiodescription: false } && !subscription.Download.DownloadAudioOnlyForPrimaryLanguage)
    || videoInfo.HasSignLanguage
    || subscription.Download.DownloadFullVideoForSecondaryAudio)
{
    return FileType.Video;
}

return FileType.Audio;
```

German content only forces `FileType.Video` when `DownloadAudioOnlyForPrimaryLanguage` is *not* set; sign-language content always forces `FileType.Video` unconditionally (moved out of the German-specific clause so it can't be bypassed by the new flag); either full-video flag still forces `FileType.Video`. Anything that falls through returns `FileType.Audio`, at which point `AudioContainerFormat` applies exactly as it does for any other audio-only trigger (secondary language, audiodescription) - no separate code path or separate format setting was introduced for the German-primary-language case. This is why the UI (Decision 9) shows a single shared `AudioContainerFormat` selector rather than one per trigger.

### 9. Single shared `AudioContainerFormat` control in the Vue.js UI, not one per trigger

`SubscriptionEditor.vue`'s Download tab shows exactly one `AudioContainerFormat` `<select>` per branch (`.strm` on/off), positioned after all the checkboxes that can lead to `FileType.Audio` (`DownloadFullVideoForSecondaryAudio`, `DownloadAudioOnlyForPrimaryLanguage`). Its helper text explicitly names all three trigger conditions (secondary language, German content with the new opt-in, audiodescription) so the user understands the one setting governs every audio-only case for that subscription. A per-trigger format chooser was considered and rejected - it would let the same subscription produce a mix of `.mka` and `.m4a` files for what the user experiences as "my audio-only downloads," with no benefit over a single setting.

## Risks / Trade-offs

- [Risk] MP4 muxer with `-movflags +use_metadata_tags` may behave inconsistently across FFmpeg versions bundled with different Jellyfin server installs. -> Mitigation: verify behavior against the FFmpeg version pinned/expected by Jellyfin server compatibility during implementation; if freeform tags are unsupported on an old FFmpeg build, the extraction still succeeds (audio + language/disposition tags), only the JSON payload embedding silently no-ops the same way partial FFmpeg failures are already logged elsewhere.
- [Risk] Vue.js config UI needs a new enum-style control (dropdown/radio) where existing sibling settings are all booleans (checkboxes) - slightly different UI pattern. -> Mitigation: use a simple `<select>` bound to the string enum value, consistent with any other existing non-boolean settings pattern in the same forms (e.g. numeric input fields), keeping the change additive and small.
- [Risk] Existing tests assert `.mka` unconditionally for the default configuration (e.g. `FileNameBuilderServiceTests.GenerateDownloadPaths_ShouldAppendLanguage_WhenNotGerman`, and the German-primary-language tests added by the merged `DownloadAudioOnlyForPrimaryLanguage` feature). -> Mitigation: since the default changes to `M4a`, these tests must be updated to expect `.m4a` for the default case; explicit-`Mka`-override tests cover the previous behavior so it isn't lost from the suite.
- [Risk] Merging two independently-developed features that both touch `GetTargetMainType`'s branching condition risks silently changing which content is treated as video vs. audio. -> Mitigation: verified via the full existing + merged test suite (201 tests passing after merge) that sign-language content still always forces video, audiodescription content still always forces audio, and the new flag only affects the German/primary-language branch, matching Decision 8's composed condition exactly.

## Migration Plan

No data migration required (per user decision: existing `.mka` files are left as-is). Deployment is a standard plugin version bump:
1. Ship the new setting on `BaseDownloadSettings` defaulted to `M4a`, alongside `DownloadAudioOnlyForPrimaryLanguage` defaulted to `false` (disabled - full video, matching prior behavior for subscriptions that don't opt in).
2. New subscriptions (and existing subscriptions whose saved config predates these fields) resolve `AudioContainerFormat` to `M4a` and `DownloadAudioOnlyForPrimaryLanguage` to `false` on load; previously downloaded `.mka` files remain untouched on disk regardless.
3. Users who want to keep `.mka` set `AudioContainerFormat: Mka` explicitly on the relevant subscription(s). Users who want audio-only downloads for German content opt in via `DownloadAudioOnlyForPrimaryLanguage: true` on the relevant subscription(s).
4. Rollback: reverting the plugin version removes both settings and restores the old hardcoded `.mka`/full-video-for-German behavior; any subscriptions already producing `.m4a` files or German audio-only downloads simply stop after rollback, with no data loss to existing files either way.
5. Call out the default change prominently in the release notes/README, since it is a behavior change for users who upgrade without reading the changelog.
