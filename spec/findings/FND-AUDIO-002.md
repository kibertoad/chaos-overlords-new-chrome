---
id: FND-AUDIO-002
title: Nine general sound effects load into slots 0 to 9 with slot 5 left empty, and a wrapper plays them only while effects are enabled
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045867C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CCF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464290
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004652A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458B05
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041953E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004196F5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048783C..0x0048783D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487864..0x00487865
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The loader `fn_0045867C` takes a memory slot from 0 to 47 and a five-digit
  sound resource number.
- The title initialization `fn_00460CCF` loads slots 0 to 4 from `SND00200` to
  `SND00204`, skips slot 5, and loads slots 6 to 9 from `SND00205` to
  `SND00208`.
- The wrapper `fn_00464290` plays a loaded slot only while the byte at
  `0x0048783C` is nonzero. It passes the slot and a priority to the lower play
  helper `fn_0045851A` (FND-AUDIO-003).
- The 110 direct calls to the wrapper pass only slots 0 to 8. None passes slot
  9.
- The Options helper `fn_004652A0` reads the effects level byte at
  `0x00487864`, sets the effects-enabled byte when the level is nonzero, and
  passes `level * 25` to `fn_00458B05`. That helper shifts the value left by
  eight and writes it into both 16-bit channels of `auxSetVolume`. Level 5 gives
  32000 in each channel and level 10 gives 64000. The initialized effects level
  is 6 (38400 in each channel); the initialized music level at `0x00487868` is 5
  (FND-AUDIO-001).
- The panel-entry helper `fn_0041953E` has 23 callers. It plays slot 0 just
  before its right-to-left copy loop, and only while the Slide Panels byte at
  `0x00487840` is nonzero. The exit helper `fn_004196F5` has the same 23
  callers and plays slot 1 before its left-to-right copy loop under the same
  test.

## Interpretation

The game keeps nine general effects: slots 0 to 4 and 6 to 9 hold `SND00200` to
`SND00208` in order, and slot 5 is free for the attack sounds of Detailed Combat
(FND-AUDIO-013). The effects level and the music level are separate settings
with separate enable flags, and both convert a level from 0 to 10 into a volume
the same way. Slots 0 and 1 are the sounds of a panel sliding in and out, so
turning Slide Panels off silences them along with the motion. Slot 9 is played
only by the direct call in FND-AUDIO-003.

## Alternatives

- The priority the wrapper passes to the lower helper has not been recorded.
- What the effects helper does at level 0 beyond leaving effects disabled (for
  example whether it still sets a volume) has not been read.
- The manual describes a Medium default for both sliders without a number; the
  initialized bytes give 6 for effects and 5 for music.

## How to reproduce

Open the title initialization at `0x00460CCF` and follow its calls to the loader
at `0x0045867C`: the resource numbers 200 to 208 appear as constants with the
slot numbers. The wrapper at `0x00464290` tests `0x0048783C` before calling
`0x0045851A`; list its callers. The Options helper at `0x004652A0` multiplies
the byte at `0x00487864` by 25 and calls `0x00458B05`, which calls
`auxSetVolume`.
