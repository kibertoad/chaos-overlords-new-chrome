---
id: FND-VIDEO-003
title: smackw32 sets a movie's volume as a linear waveOut volume, clamped to 0xFFFF, both channels equal at the centre pan
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: SMACKW32.DLL
    address: 0x00402D50..0x00402DED
  - build: BLD-GOG-EN-1.1
    file: SMACKW32.DLL
    address: 0x00404D7E..0x00404D8C
  - build: BLD-GOG-EN-1.1
    file: SMACKW32.DLL
    address: 0x004072D0..0x00407341
  - build: BLD-GOG-EN-1.1
    file: SMACKW32.DLL
    address: 0x00407350..0x004073CC
tool: Python 3.14.7 script disassembling the file bytes with Capstone 5.0.7
environment: null
---

## Observation

Addresses are virtual addresses of `SMACKW32.DLL` at its image base
`0x00400000`.

- `SmackVolumePan`, the export with ordinal 31 at `0x00402D50`, clamps its
  volume and its pan arguments to `0x10000` each and, for every bit of the
  track mask from `0x2000` up, it takes the track's record from offset `0x400` of
  the movie and, when the record and the function pointer held at `0x0040E774`
  are both set, calls that pointer with the pan, the volume and the record.
- When a movie's sound is opened and the pointer at `0x0040E768` is 0, the call
  at `0x00404D87` runs the installer at `0x004072D0`, which fills the sound
  backend's pointers, among them `0x0040E768`, and stores `0x00407350` at
  `0x0040E774`. The only other writes to `0x0040E768` and `0x0040E774` are in
  `SmackSoundUseMSS` (ordinal 49, `0x004059A0`) and
  `SmackSoundUseDirectSound` (ordinal 48, `0x004064E0`), and the executable
  imports neither.
- `0x00407350` clamps the volume to `0xFFFF`. For a pan of `0x8000` or more the
  right channel is the volume; for a pan above `0x8000` the left channel is the
  volume scaled down by the pan, and at `0x8000` exactly it is the volume. A pan
  below `0x8000` scales the right channel the same way. It then looks up the
  device of the wave handle at offset `0x38` of the record with `waveOutGetID`
  (import thunk `0x0040CFFC`) and calls
  `waveOutSetVolume` (import thunk `0x0040CFF6`) with the left channel in the
  low word and the right channel in the high word.

## Interpretation

With the pan `0x8000` the game always passes (FND-VIDEO-002), a movie's volume
`v` sets both channels of the wave device to `min(v, 0xFFFF)`. The game passes
`effects_level * 25 * 256`, so each of the eleven Sound Effects levels gives a
channel value of `effects_level * 6400`, from 0 at level 0 to 64,000 at level
10, and no level reaches the clamp. `waveOutSetVolume` takes 0 as silence and
`0xFFFF` as full volume on a scale Windows applies linearly to the samples, so
the movie's gain is `effects_level * 6400 / 65535`.

## Alternatives

- A driver that does not support a volume per channel applies only the low
  word, which here is the same value.
- The volume set on the device applies to every sound played through it while
  the movie plays; the intro plays nothing else.

## How to reproduce

In `SMACKW32.DLL`, find the export with ordinal 31 and follow its indirect call
through `0x0040E774`. Find the write of `0x00407350` to `0x0040E774` in
`0x004072D0` and the test of `0x0040E768` before the call at `0x00404D87`. In
`0x00407350`, find the comparisons with `0xFFFF` and `0x8000` and the two
thunk calls, and resolve the thunks through the import table to `WINMM.DLL`.
List the imports of `Chaos Overlords.exe` from `smackw32.dll`.
