# Audio and video

Status: active media map
Last updated: 2026-09-23

All files described here come from a user-owned, extractor-verified original
asset pack. None are distributed by this repository.

## Sound effects

The extractor copies 28 `SND*.wav` resources without changing their bytes.
The client keeps combat sounds separate from the recovered nine-entry general
sound table. Combat events retain the equipped weapon sound at resolution time;
unarmed and Martial Arts attacks use indices 0 and 1, detected police use 18,
and evasion is silent. Detailed playback triggers a sound on the first frame of
its corresponding animation clip, including delayed retaliation.

The general table maps `SND00200` through `SND00208`; slot 5 is intentionally
absent in the source table. Confirmed ownership is:

| Slot | Current owner |
|---:|---|
| 0/1 | Sliding panel entry/exit while panel motion is enabled |
| 2 | Pointer-button press for recovered full controls and main-console families |
| 3 | Accepted selection, confirmation, and bounded page/selection movement |
| 4 | Rejected selection and attempts to move beyond a page boundary |
| 6 | Unread Comlink entry and four-second reminder cadence |
| 7/8 | Planning-clock warnings |
| 9 | Turn-start cue after the initial turn in local and legacy-network play; calls the lower playback helper directly |

Effects use an independent 0-10 preference, default 6, and the recovered
amplitude conversion. The original's `PlaySoundA` call omits `SND_NOSTOP`,
allowing a new effect to interrupt the previous one. The client uses a single
effect voice for general and combat cues, separate from music. Native timing,
priority handling, and any unmapped call sites remain evidence work.

## Music

The GOG pack supplies eight Ogg files named `Track02.ogg` through
`Track09.ogg`. The title and setup repeat Track 2, ordinary gameplay advances
through Tracks 3-8 and repeats, and endgame repeats Track 9. Losing focus pauses
music and regaining focus resumes it. Music has its own 0-10 preference,
default 5, and degrades to silence without affecting simulation when a backend
or file is unavailable.

## Smacker video

Both source movies use the `SMK2` signature and little-endian fixed header.
`SmackerVideoReader` bounds dimensions and frame counts, decodes both Smacker
frame-duration encodings, identifies packed PCM track flags, accounts for an
optional ring frame, and proves that the fixed header, frame-size/type tables,
Huffman trees, and all declared frame payloads exactly cover the file.
`SmackerFrameDemuxer` then bounds and separates each physical frame's optional
palette update, ordered audio chunks, and remaining video bitstream.
`SmackerPaletteDecoder` applies the format's skip, previous-palette copy, and
six-bit BGR color commands while bounding every source and destination range.
The managed audio decoder reconstructs the movies' packed unsigned 8-bit mono
and stereo PCM, including per-chunk Huffman trees, channel predictors, and the
codec's intentional byte wraparound. Source verification decodes every audio
chunk rather than trusting container metadata alone.
`SmackerVideoDecoder` builds the four bounded canonical Huffman codebooks and
reconstructs every 4x4 monochrome, full, skipped, or filled block into indexed
pixels while preserving inter-frame state. Source verification now decodes all
1,350 physical video frames as well as their palette and audio data.

| File | SHA-256 | Size | Video | Timing | Audio |
|---|---|---:|---|---|---|
| `MVLOGOS.smk` | `cd5b80b01626155ec1b7be0c879378117f2323ed9ec824a704e4ca08c1974752` | 1,832,372 | 480x256, 200 frames | 100 ms/frame, 20 s | packed PCM, 22,050 Hz, 8-bit mono; max decoded frame 24,260 bytes |
| `MVINTRO.smk` | `52478e0fbcd0fc39bb31171d2dc86115a2b685cc930107587c6a7dba496a77dd` | 8,592,724 | 480x256, 1,150 frames | 100 ms/frame, 115 s | packed PCM, 22,050 Hz, 8-bit stereo; max decoded frame 48,512 bytes |

The structural metadata above is Verified against the fingerprinted GOG pack.
The extractor validates both containers before accepting the source pack.

### Playback decision

The recreation decodes the validated Smacker-v2 subset in managed runtime code.
It does not require an ambient FFmpeg installation, emit thousands of derived
frame files, or depend on host codecs. The raw user-owned `.smk` files remain in
the local extracted pack. Playback streams `MVLOGOS.smk` and then `MVINTRO.smk`,
centers each native 480x256 indexed frame in the 640x460 virtual canvas, advances
on a deterministic 100 ms presentation timeline, and submits decoded PCM to the
runtime audio backend. Losing focus pauses movie audio. Escape, Enter, Space,
or either mouse button skips only the current movie and consumes that input.

The movies stream unattended only while the client preferences have not recorded
a showing. Draining the queue after at least one movie opened, whether it played
out or was skipped, records `IntroMoviesSeen` there, so every later run of that
installation opens at the title screen instead. A pack whose movies all fail to open shows
nothing and leaves the flag unset. The title screen's `INTRO` button replays the
same queue on demand and reports `INTRO VIDEO UNAVAILABLE` when no movie file can
be read. A replay silences the menu music for its duration; the first update
after the queue drains restarts the track list the current screen calls for.

Missing files and decoder, texture, or audio failures skip the affected movie
and continue through the queue to the title. Playback state never enters saves,
replays, phase hashes, or multiplayer state. Policy, cadence, ordering, and
skip/failure behavior have focused tests. A bounded Windows smoke run completed
the logo and began the intro without a diagnostic failure; exact original
trigger/skip policy and native visual/audio fidelity still require reference
capture before they can be classified as parity.
