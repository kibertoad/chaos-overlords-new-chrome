---
id: FND-PLATFORM-006
title: Sound, CD music, timers and Smacker video come from WINMM and smackw32.dll
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE8EC..0x004AE918
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE974..0x004AE9B4
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `WINMM.dll` supplies eleven imports at `0x004AE8EC..0x004AE918`:
  `PlaySoundA`, `mciSendCommandA`, `auxGetNumDevs`, `auxGetDevCapsA`,
  `auxGetVolume`, `auxSetVolume`, `timeGetTime`, `timeSetEvent`,
  `timeKillEvent`, `timeBeginPeriod` and `timeEndPeriod`.
- `smackw32.dll` supplies sixteen imports, all by ordinal (1, 3, 7, 8, 9, 14,
  15, 17, 20, 21, 22, 26, 28, 29, 31 and 32), at `0x004AE974..0x004AE9B4`.

## Interpretation

Sound effects are played with `PlaySoundA`, CD music is driven through MCI,
and volume is set on the auxiliary devices; FND-AUDIO-001 and FND-AUDIO-002
trace these calls. The two movies are decoded by RAD's Smacker library, which
ships with the build as `SMACKW32.DLL`. The multimedia timers drive animation
and sound timing; their presence alone does not show that game logic depends
on them.

## Alternatives

The Smacker ordinals have not been matched to the library's function names.

## How to reproduce

List the imports of `WINMM.dll` and `smackw32.dll` in `Chaos Overlords.exe`.
