# Native backend checks

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

## Native audio backend

The native soundtrack EOF regression pins the old stream at its decoded end before
replacing its program, then requires the replacement decoder to reach the end of a
2.5-second synthetic track. This checks stale completion-state truncation, without
proving audible output or final partial-buffer delivery. The production adapter
binds MonoGame DesktopGL 3.8.5.1 `OggStreamer.Instance` and `pendingFinish` through
reflection: explicit stop removes and synchronizes with the old stream before the
flag is reset, and the new stream is published afterward. A paused resume retains
its existing completion state. Dependency upgrades must preserve these boundaries
or replace this hook with an equivalent supported API.

A separate native diagnostic confirms a remaining tail-delivery defect in the pinned
backend. Keep the initially prepared 0.5-second buffer playing with native looping,
seek the decoder of the synthetic 2.5-second track to 2.25 seconds, and wait for
`pendingFinish`. The decoder reaches 2.5 seconds, but `ALGetSourcei.BuffersQueued`
is 1 rather than 2: the decoded 0.25-second tail was not queued. The streaming
worker sets `finished` on that read and queues buffers only when `!finished`.
This diagnostic intentionally changes decoder position to isolate submission; it
does not compare audible hardware output. Whole-track decoding alone therefore
cannot establish the endpoint required by RULE-AUDIO-001. Repairing final-buffer
submission remains necessary before claiming full soundtrack parity.

## Native pattern fill reference

`NativePatternFillTests` compares the visible pixels from the production
pattern-fill helper with Windows `Rectangle` in a new memory DC, which holds the
default black pen, filled with a solid white brush and with a solid black brush,
the two fills the game uses (FND-GFX-006). It covers the current production
rectangle sizes, smaller examples and one-pixel-wide strips, without original
assets. A separate case records that Windows leaves the initialized scratch bitmap
of a 1-by-1 rectangle unchanged while the helper draws it black; no current game
caller fills that size, and the helper parity claim excludes it. The tests check
fill and outline pixels only. The compositor raster operations and screen
rendering are unverified. They skip outside Windows.

## Original pattern resources

`OriginalPatternResourceTests` reads the hash-verified original executable's bytes, walks its PE resource directory for bitmap resources 143, 146 and 147, checks their headers and black and white palettes, and compares all 192 mask bits with the production pattern helper (FND-UI-031, FND-GFX-006). It does not load or execute the original, so it runs on every platform, and it does not store resource bytes in the repository. It requires `GAME_DIR` and skips when the executable is unavailable. This verifies mask shape, palette and row orientation, not raster-operation compositing or rendered-screen parity.
