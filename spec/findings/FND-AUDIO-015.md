---
id: FND-AUDIO-015
title: Title music is requested after successful game entry and return, not after cancelled preparation or loading
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00461763..0x004617C2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004619AB..0x00461A3B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00461B91..0x00461BF1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00461E87..0x004620A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004637B8..0x0046381A
tool: Ghidra 12.1.3
environment: null
---

## Observation

The executable matches BLD-GOG-EN-1.1. This follows the branch conditions
around the five title-return selector calls listed in FND-AUDIO-001, within
`fn_00460CCF` (FND-EXE-004).

- Local preparation: the call to `fn_0040E0A0` at `0x00461763` is followed by
  a zero-result branch at `0x0046176E` to `0x004617C2`. That target is past the
  title selector at `0x004617BA`. A nonzero result enters `fn_0046E766` before
  returning through the title selector, provided `g_00487828` is zero.
- Host preparation: the call to `fn_004677F0` at `0x004619AB` is followed by
  a zero-result branch at `0x004619B9` to `0x00461A3B`. This skips both the game
  loop and the selector at `0x00461A33`.
- Join preparation: the call to `fn_0040B9C0` at `0x00461B91` is followed by
  a zero-result branch at `0x00461B9F` to `0x00461BF1`. This skips both the game
  loop and the selector at `0x00461BE9`.
- The title Load command calls `fn_004637B8`. Its return starts at zero and
  changes only when `fn_0042AFDD(3)` accepts a file and `fn_0046381A` reads it.
  A zero return leaves the title's pending action unchanged. Results 1 and 2
  schedule the local-load and network-load paths respectively. The local-load
  path calls the game loop before the selector at `0x00461ED6`. The network-load
  path calls `fn_00456F80`, enters the game only after its nonzero result, and
  calls the selector at `0x00462098` after that game returns. Cancellation of
  the title Load dialog does not reach either selector.

The host preparation return is initially zero, and the joining preparation
also leaves its return zero on cancellation. FND-NET-003 and FND-NET-004 place
these preparation screens and distinguish them from entering a game.

## Interpretation

Returning to the title after a game asks for the title program again. Simply
backing out of local or network preparation, or cancelling the title Load
dialog, keeps the already-playing title program without a new selector call.
FND-AUDIO-001's phrase about network and load paths returning to the title
must not be read as applying to every cancelled preparation or load dialog.

## Alternatives

This establishes the executable's calls, not the replacement GOG wrapper's
audio behavior. A restart caused independently by the stopped-playback poll
or a window activation remains possible (RULE-AUDIO-002).

## How to reproduce

Verify BLD-GOG-EN-1.1, then inspect the zero-result branches before each title
selector in `fn_00460CCF`. Follow the Load command through `fn_004637B8` and
its pending-action switch before the two load-related return selectors.
Use bounded instruction windows to check the branch targets as well as the
focused decompilation. Do not infer a selector call merely from a dialog
returning to the title.