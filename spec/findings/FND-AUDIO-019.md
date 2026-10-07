---
id: FND-AUDIO-019
title: Only the level setup writes effects_enabled, and the wrapper's one call of the play helper is at 0x004642AB
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048783C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487838..0x0048783D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464290..0x004642BC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004652A0..0x004653AD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460D14
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00461088
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462912..0x00462924
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487864
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. The references were listed with
Ghidra's reference manager after the default analysis.

- The executable has three references to the `effects_enabled` byte at
  `0x0048783C` (FND-AUDIO-002): the read at `0x00464298` in the effects wrapper
  `fn_00464290`, and two byte writes in the level setup `fn_004652A0`, 0 at
  `0x004652B8` and 1 at `0x004652C4`. No instruction refers to `0x00487839`,
  `0x0048783A`, `0x0048783B` or `0x0048783D`, and every reference to the
  `music_enabled` byte at `0x00487838` is a byte access, so no wider access to
  a neighbour reaches `0x0048783C`. The image holds 1 at `0x0048783C` and at
  `0x00487838`.
- `fn_004652A0` reads the effects level `0x00487864` as a signed byte at
  `0x004652A9` and writes 0 to `0x0048783C` when it is 0, otherwise 1, before it
  sets the effects volume (FND-AUDIO-007).
- The wrapper `fn_00464290(slot)` loads `0x0048783C` at `0x00464298`, and when
  it is 0 jumps from `0x0046429F` past the call. Otherwise it pushes 1 and the
  slot and calls the play helper `fn_0045851A` at `0x004642AB`, which returns
  to `0x004642B0`. That is the wrapper's only call; it has no other path to
  the helper.
- `fn_004652A0` has four references, all direct calls, as FND-AUDIO-007 lists:
  `0x00461088` in the title initialization `fn_00460CCF`, the Music and Sound
  Effects menu commands at `0x0046266B` and `0x00462683`, and the window
  activation at `0x0046291F`. The activation call is reached only when the
  byte at `0x00487838` is nonzero (the test at `0x00462912` and the jump at
  `0x00462919`).
- In the title initialization the call of the preference loader `fn_0046439A`
  at `0x00460D14` comes before the level setup's call at `0x00461088`. The
  loader writes the effects level at `0x004645AA`; the only other write of
  `0x00487864` is the Sound Effects menu command at `0x0046267E`.

## Interpretation

`effects_enabled` changes only inside the level setup, so its value after the
title initialization's level setup is the value at any later point unless a
later call of the level setup changes it: following each call of the level
setup gives the setting at every moment of a run. A value written into the
effects level after the preference loader returns, as a debugger can write it,
is what the title initialization's level setup derives the setting from.

A call of the play helper whose return address is `0x004642B0` is a request
that went through the wrapper; any other caller of the helper bypasses the
effects setting (FND-AUDIO-006).

## Alternatives

- A write through a pointer computed at run time, or a block copy over the
  globals, would not show as a reference, and this reading cannot rule one
  out. In a run it would show as a value read at a Done press that the level
  setups before it did not leave.

## How to reproduce

List the references to `0x0048783C` and to each of `0x00487838` to
`0x0048783D`, and the references to `0x004652A0` and `0x00487864`. Read the
instructions from `0x00464290` to `0x004642B9` and from `0x004652A9` to
`0x004652C4`, and those around `0x00460D14`, `0x00461088` and `0x0046291F`.
Read the byte at `0x0048783C` in the image.
