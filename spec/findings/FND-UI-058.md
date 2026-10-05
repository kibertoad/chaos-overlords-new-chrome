---
id: FND-UI-058
title: A byte marks the match as saved; a save or a load sets it, a resolved turn and each accepted order clear it, and File, End and File, Exit offer dialog 129 while it is clear
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464AE6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470435
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047052B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047091A
tool: Ghidra 12.1.3
environment: null
---

## Observation

Every write to the byte at `0x00498350` but two goes through
`fn_00464AE6(value)`. That function stores the value, or 1 when the network
byte `0x00482178` is set, and then greys File, Save when the stored value is 1.
The two direct stores of 1 are at title start, `0x004614A8`
(FND-PLATFORM-009), and where the match loop exits, `0x0046F9D6`.

It is set to 1:

- by the save `fn_00463CC5`, at `0x004640C1`, after the file is written;
- by the load `fn_0046381A`, at `0x00463CA4`;
- by the two title paths that set `0x00487B98` and enter the match with 0,
  at `0x00461BB5` and `0x00462208`.

It is set to 0:

- by the three title paths that start a new game, at `0x00461790`,
  `0x004619DB` and `0x00462040`;
- by the match entry after each turn's resolution, at `0x0046F6FE`, just before
  `fn_004726C0` starts the next planning;
- by the drag handler `fn_00416C75` after each drop it accepts, at `0x00416E9D`,
  `0x00417150`, `0x0041796F`, `0x00417B85` and `0x00417C3B`;
- by `fn_0041462F`, at `0x00414D65`;
- by the gang command box `fn_00414D8C` when it stores an order or clears one,
  at `0x00415ABA` and `0x0041670C`.

Planning's command handler in `fn_0046FD80` reads it for File, End (`0x81`,
4), at `0x00470435`, and for File, Exit (`0x81`, 9), at `0x0047052B`:

- When the byte is 0 and `0x004ABC9C` is 0, it opens dialog 129 through
  `fn_00465CEC`.
  - Answer 1 runs the save `fn_00463CC5`. When the save returns nonzero it
    runs `fn_0046D00D`, and when that returns nonzero it sets `0x004ABC90`.
  - Answer 2 does nothing more.
  - Answer 3, like any answer other than 1 and 2, runs `fn_0046D00D` and, when
    that returns nonzero, sets `0x004ABC90`.
- Otherwise it runs `fn_0046D00D` and, when that returns nonzero, sets
  `0x004ABC90`.

File, Exit does the same and also sets `0x00487828` wherever it sets
`0x004ABC90`. `fn_0046D00D` is the network disconnect, which returns 1 at once
in a local game (FND-NET-004).

A third copy of the End test, at `0x0047091A`, sits in the loop that waits for
the CD test `fn_0046638E` to pass. That test always passes (RULE-AUDIO-010), so
the copy is never reached. The same test of the byte and dialog 129 appear in
the hot-seat handoff `fn_004396C0` (`0x00439C57`, `0x00439D58`) and in
`fn_00471F06` (`0x00472189`, `0x00472279`).

## Interpretation

The byte, `match_saved`, says whether the match is as it was last saved or
loaded. A new match starts unsaved. A drop or a command box order makes it
unsaved, and so does the resolution of a turn, even with no order given. Ending
the match or quitting while it is unsaved asks first: save, cancel, or go on
without saving. A save that fails or is cancelled leaves the player in planning.
While no match is in play the question is never asked, and in a network game
the byte is always 1, so it is never asked there either.

## Alternatives

What `fn_0041462F` does around its clear was not followed further; the reading
of the End and Exit branches does not depend on it.

## How to reproduce

List the callers of `0x00464AE6` and the writes to `0x00498350`, and read the
branches at `0x00470435` and `0x0047052B` in `fn_0046FD80`.
