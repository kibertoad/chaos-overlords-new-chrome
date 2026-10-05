---
id: FND-AUDIO-017
title: The CD fade runs inside the event pump's music poll and mute command, and never touches timer slot 0
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046438B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004642D5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046533A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046101A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046224F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462AE8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046266B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462683
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046291F
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004.

- The fade `fn_00458D54` (FND-AUDIO-016) has one call, at `0x0046438B` in
  `fn_00464385`.
- `fn_00464385` is called by the music selector `fn_004642BD` (`0x004642D5`),
  by the muting branch of `fn_004652A0` (`0x0046533A`) and twice by the title
  loop `fn_00460CCF` (`0x0046101A`, `0x0046224F`).
- The event pump `fn_00462579` calls the selector at `0x00462AE8`, the music
  poll of FND-AUDIO-007, and `fn_004652A0` at `0x0046266B`, `0x00462683` and
  `0x0046291F`. The selector's other callers are `fn_0042B9E0`, `fn_0042C3F5`,
  the title loop and the outer match loop `fn_0046E766`.
- No chain of direct calls of up to eight steps leads from `fn_00464385` or
  `fn_00458D54` to `fn_004328BE`, which reads a timer slot's flag,
  `fn_004328F8`, which clears it (FND-UI-023), or the pump.

## Interpretation

A fade the pump starts runs inside that pump call, so the pump takes no
presentation tick until the fade returns: the planning bar, the Comlink blink
step and the alert repeat stop for it. The fade neither reads nor clears timer
slot 0, so its flag keeps one of the ticks that fell during the fade and the
pump takes it on its next call; the others are lost. The fade's last wait ends
527 ms after it starts (FND-AUDIO-016), so at least three ticks fall during a
fade and at least two are lost. Whichever caller starts it, the fade calls no
pump and leaves slot 0 alone, so a fade from another caller stops the pump's
steps in the same way.

## Alternatives

The count of lost ticks assumes the fade takes about its nominal length; a slow
CD device or a long message dispatch lengthens it and loses more.

## How to reproduce

List the callers of `0x00458D54`, `0x00464385`, `0x004642BD` and `0x004652A0`.
Look for call paths from `0x00464385` and `0x00458D54` to `0x004328BE`,
`0x004328F8` and `0x00462579`.
