## Purpose

Defines the behavior of audio-only content exports (secondary-language audio extraction without video), including the selectable output container format, codec handling, and embedded metadata, so that extracted audio files are correctly playable and identifiable both inside Jellyfin and in external audio/podcast players.

## ADDED Requirements

### Requirement: Configurable Audio Container Format

The system SHALL support two audio-only output container formats: Matroska Audio (`.mka`) and MPEG-4 Audio (`.m4a`). Each subscription SHALL have its own `AudioContainerFormat` setting, which defaults to `.m4a` when a subscription is created without an explicit value, consistent with how other download settings on a subscription behave. The Vue.js configuration UI's "subscription defaults" pre-fill mechanism MAY be used to pre-populate this value when a user creates a new subscription, but the setting itself is a plain per-subscription value with no runtime fallback to a separate global setting.

#### Scenario: Default format is M4A

- **WHEN** a new subscription is created without explicitly setting the audio container format
- **THEN** the subscription's audio container format is `.m4a`, and the system extracts audio-only downloads for that subscription as `.m4a` files

#### Scenario: Subscription explicitly set to Matroska Audio

- **WHEN** a subscription's audio container format is explicitly set to `.mka`
- **THEN** the system extracts audio-only downloads for that subscription as `.mka` files, independent of any other subscription's setting

### Requirement: Lossless Codec Passthrough

The system SHALL extract the source audio stream without re-encoding (codec passthrough) regardless of which output container format is selected.

#### Scenario: Audio extraction does not transcode

- **WHEN** an audio-only download is executed for either `.mka` or `.m4a` output
- **THEN** the resulting audio stream is a direct copy of the source audio codec, with no transcoding step performed

### Requirement: Language and Disposition Tagging

The system SHALL tag the extracted audio stream with its language code, and SHALL mark the stream's disposition (original-language and/or audio-description, as applicable) in both supported container formats.

#### Scenario: Language tag present in both formats

- **WHEN** an audio-only download completes for either `.mka` or `.m4a` output
- **THEN** the resulting file's audio stream metadata includes the correct 3-letter language code

#### Scenario: Original-language and audio-description disposition preserved

- **WHEN** the source audio is the original-language track and/or an audio-description track
- **THEN** the resulting file's audio stream disposition reflects those flags, regardless of the selected container format

### Requirement: Embedded Item Metadata for Audio Extraction

The system SHALL embed the plugin's item metadata payload (identifying the source item, download URL, title, topic, and related descriptive fields) into audio-only output files, using a mechanism appropriate to the selected container format, so that the embedded metadata is available for both `.mka` and `.m4a` outputs.

#### Scenario: Metadata embedded in Matroska audio output

- **WHEN** an audio-only download is extracted as `.mka`
- **THEN** the resulting file contains the plugin's item metadata payload readable from the container's tag storage

#### Scenario: Metadata embedded in MPEG-4 audio output

- **WHEN** an audio-only download is extracted as `.m4a`
- **THEN** the resulting file contains the plugin's item metadata payload readable from the container's tag storage

### Requirement: No Migration of Existing Files

Changing a subscription's audio container format setting SHALL NOT affect files that were already downloaded before the setting was changed. Only downloads performed after the setting takes effect SHALL use the newly selected format.

#### Scenario: Existing downloads remain unchanged after setting change

- **WHEN** a subscription's audio container format setting is changed after audio files have already been downloaded in the previous format
- **THEN** previously downloaded files are left untouched and are not converted or renamed

### Requirement: Library Recognition of Supported Audio Formats

The system's local media scanning SHALL recognize both `.mka` and `.m4a` as valid audio file extensions.

#### Scenario: M4A file recognized during library scan

- **WHEN** the local media scanner encounters a file with the `.m4a` extension
- **THEN** the file is recognized and processed as a valid audio media file
