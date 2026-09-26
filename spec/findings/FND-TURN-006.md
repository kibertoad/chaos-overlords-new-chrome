---
id: FND-TURN-006
title: The outer turn loop skips both the recurring cleanup and Upkeep on its first pass, rebuilds visibility once before planning, and counts elapsed_turns after resolution
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766..0x0046FA10
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0049CA68
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBE0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB668
tool: Ghidra 12.1.3
environment: null
---

## Observation

All addresses are in `fn_0046E766` (range in FND-EXE-004).

Before the fresh-game test, `0x0046E89B` sets a local first-pass flag to 1.
The test at `0x0046EB4E` then either initializes a new match (the path that
sets `0x0049CA68` to 0 at `0x0046EB56`, sets each player's active byte at
`0x004ABBE0 + player` to 1 at `0x0046EB91`, and calls `fn_0046DC10` at
`0x0046EC40`) or, for a loaded match, calls `fn_0040AAE3` and `fn_0046D22F`.
Both paths reach the loop at `0x0046ED32`, which runs while the quit flag
`0x00487828` and the end flag `0x004ABC90` are 0.

Each pass of the loop does the following, in address order:

1. Only when the first-pass flag is 0 (test at `0x0046ED4E`): the recurring
   cleanup over 6 players and 81 roster slots (`0x0046ED56..0x0046EFE5`,
   FND-TURN-004), then the Upkeep and income scans (`0x0046EFFF..0x0046F1CE`).
   The first-pass flag is cleared at `0x0046F209`, after this block.
2. The sector rebuild: each of the 64 sector records is passed to
   `fn_004782C5` (call `0x0046F23F`) and the result copied back.
3. The gang rebuild: each gang whose sector byte is not 100 is passed to
   `fn_0047781F` (call `0x0046F2FD`).
4. One call of the visibility rebuild `fn_0046FA11`, at `0x0046F344`.
5. A count of human slots (`0x0046F349..0x0046F3C2`): a slot whose controller
   (`0x004AB638`) is 0 counts; one whose controller is 0 and whose active byte
   is 0 also counts and has its controller set to -2 at `0x0046F3B7`. With more
   than one, `0x004ABC98` is set. In a local match with a count of 0 the end
   flag is set at `0x0046F42A`.
6. The six bytes at `0x004ABC88` are cleared (`0x0046F431..0x0046F44D`).
7. The planning loop, slots 0 to 5 (`0x0046F459..0x0046F5E0`). A slot enters
   only while the end and quit flags are 0 and its active byte is nonzero or
   its controller is -2. Controller 1: `active_player` is set, the offer refill
   `fn_004716EB` is called at `0x0046F4E3` and the computer planner
   `fn_00458FA0` at `0x0046F4EC`. Controller 0: `fn_004396C0` when
   `0x004ABC98` is set, then `active_player`, then the human handler
   `fn_0046FD80` at `0x0046F552`, then `fn_0046CF38`. Controller -2:
   `fn_004396C0` when `0x004ABC98` is set, then `fn_0042C3F5`, and the
   controller becomes -1 at `0x0046F597`. There is no branch for controller 3
   or -1.
8. Network handoff (`0x0046F5E7..0x0046F679`), only when `0x00482178` or
   `0x00487B58` is set.
9. `fn_004726C0` at `0x0046F706`, which calls the resolver
   (FND-TURN-005), when the quit and end flags are 0 and the human count is
   nonzero or `0x00487B58` is set.
10. When `0x004ABBD4` is nonzero (test at `0x0046F712`), the match-end branch:
    the sector and gang rebuilds again, the action byte of every gang whose
    sector byte is not 100 set to 8 at `0x0046F87B`, the visibility rebuild at
    `0x0046F896`, and a loop over slots 0 to 5 (`0x0046F89B..0x0046F91F`). For
    each slot whose controller is 0 it calls `fn_004396C0` when `0x004ABC98`
    is set, then `fn_0042C3F5` when the active byte is 0, or `fn_0046FD80`
    with `0x0048780C` set to 1 around the call otherwise. Then `fn_0042B9E0`
    is called and the end flag set. `0x004ABBD4` is written only by the end
    evaluator `fn_00476857`, apart from the two clears in this function.
11. `INC dword ptr [0x0049CA68]` at `0x0046F930`, on every pass that gets
    there, including the last.

Besides this function's clear and increment, `0x0049CA68` is written directly
only by `fn_0046A115` at `0x0046A1B7`; `fn_0046381A`, `fn_00463CC5` and
`fn_0046A115` also pass its address to other code. Its readers include the Crackdown window of the resolver
(`0x0047343D`, `0x00473483`) and the Crackdown history stamps
(`0x0047369A..0x00473783`).

The recurring cleanup reads, for Influence, the sector bytes `+7 + 2 × k`
and `+8 + 2 × k` with `k` the gang's `repeat_target`, and the word at
`0x004AB67E + definition × 0x3E`; for Research, the byte at `0x004A2608 +
repeat_target × 6 + player`; for Control, the sector's presence byte at
`0x0046EE04`. It writes only the gang bytes `+7`, `+8` and `+10`.
`fn_0046E766` has no write to any sector's presence byte (record offset
`0x0F`); the only decrement is at `0x00475E74` in the resolver (FND-POLICE-004).

Earlier in the function, the files `data\Gangs`, `data\Items` and `data\Sites`
are read whole into `0x004A2800` (`0x36D8` bytes), `0x004A5F08` (`0x2980`
bytes) and `0x004AB668` (`0x554` bytes) respectively.

## Interpretation

The first pass of the loop, for a new match and for a loaded one alike, skips
the recurring cleanup as well as Upkeep and income. A match loaded from a save
therefore starts its first turn with the orders and cash the save holds, and
the recurring orders are only renewed from the second pass.

Visibility is rebuilt once per turn, after the sector and gang rebuilds and
before any player plans; the offer refill comes later, at each computer
player's planning entry, and inside the human handler for a human.
Controller 3, a remote human, has no planning branch in this loop.

A human who has just been eliminated gets one last call of `fn_0042C3F5` in
the planning loop and is then marked -1 and skipped. A local match with no
human left ends before resolution.

The second slot loop runs only when the match has ended: it shows each local
human the final state. It is not part of an ordinary turn.

`elapsed_turns` counts completed resolutions: it is 0 during the first turn's
planning and resolution, and is incremented after the resolver returns and
before the next turn start. The Crackdown window compares with this counter.

There is one Crackdown-duration update, the decrement near the end of the
resolver. The mention in FND-TURN-004 of an update in this function is the
Control test's read of the presence byte.

The site definition table (`site_definitions`) starts at `0x004AB668`: 22
records of `0x3E` bytes read from `data\Sites`, so `0x004AB67E` is the
`resistance` field (+`0x16`) and `0x004AB684` the `tolerance` field (+`0x1C`)
of FMT-DATA-001.

## Alternatives

What `fn_0042C3F5`, `fn_004396C0`, `fn_0046CF38` and `fn_0042B9E0` show has
not been read. Why the match-end branch sets every active gang's action to 8
has not been traced to a consumer.

## How to reproduce

In `fn_0046E766`, find the store of 1 to a local byte at `0x0046E89B` and its
test at `0x0046ED4E`, which guards both the recurring switch and the Upkeep
loops; the store of 0 at `0x0046F209` follows them. Follow the calls at
`0x0046F23F`, `0x0046F2FD` and `0x0046F344`, the controller tests of the
planning loop from `0x0046F459`, the call at `0x0046F706`, the test of
`0x004ABBD4` at `0x0046F712`, and the increment at `0x0046F930`. List the
references to `0x0049CA68` and `0x004A08F7`.
