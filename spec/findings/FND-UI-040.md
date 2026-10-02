---
id: FND-UI-040
title: Planning entry positions the scenario, calendar and player totals in the console
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FD80
tool: Ghidra 12.1.3
environment: null
---

## Observation

The planning-entry function `fn_0046FD80` draws into surface 1:

- It loads string resource `g_004ABBE8 + 1` and draws it at `(481,6)`.
- It draws `g_0049CA68 / 52 + 2050` at `(481,15)` as four numeric cells,
  without leading zeroes, and `g_0049CA68 % 52 + 1` at `(511,15)` as two
  cells with leading zeroes.
- When `g_004ABC9C` is zero and `g_004ABBE8 < 4`, it draws
  `g_004A5EF8 - g_0049CA68 - 1` at `(562,15)` as three numeric cells,
  without leading zeroes. When `g_004ABC9C` is nonzero it instead loads
  string resource 19 and draws it at `(532,15)`.
- It draws the active player's integer at `g_004A2790 + 4 * g_004ABC84`
  at `(550,24)` as five numeric cells without leading zeroes, and the
  integer at `g_004A25E8 + 4 * g_004ABC84` at `(550,42)` with the same
  width and zero policy.

The string and numeric calls use the helpers described by FND-UI-019 and
FND-UI-004. Five six-pixel cells occupy x 550 through 579 inclusive.

## Interpretation

The scenario starts at `(481,6)`. The calendar has separate year and week
fields. The player score and cash fields share the same five-cell origin;
their right boundary is x 580, exclusive. FND-STATE-010 and
FND-OBJECTIVE-004 identify the player fields.

FND-STATE-010 identifies `g_004ABC9C` as set once a match has ended, so
resource 19 is the final-view caption for every scenario. FND-OBJECTIVE-003
identifies `g_004A5EF8` as `turn_limit` and `g_0049CA68` as
`elapsed_turns`, so during play the four timed scenarios show the turns left
after the one being planned.

## Alternatives

This reading establishes the planning-entry calls, not every later refresh
path. The resource text is not reproduced here.

## How to reproduce

Verify the executable against BLD-GOG-EN-1.1, then inspect `fn_0046FD80`
in Ghidra. Follow the text and number calls immediately after the console
initialization and before the selected-sector refresh. Decode the point
arguments and retain the numeric width and leading-zero arguments. Read
string resource 19 from the executable's string table (resource type 6).
