---
id: FND-AI-027
title: Sector selector modes 10 to 16 and encoded modes, and the filters applied after scoring
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004093D3..0x004099AE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004099F8..0x00409B6D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004045BB..0x0040465C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00404539..0x004045B7
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the shared sector selector `0x00408642`:

- Mode 10 with no human players (selector `0x32` is 0) scores every sector
  owned by a player other than the active player +1; with at least one human
  player it scores every human-owned sector +1. Its own test does not read the
  attitude matrix.
- Mode 11 scores the sector returned by selector `0x5A` +1.
- Modes 12 and 14 admit only the sectors 27, 28, 35 and 36; modes 13 and 15
  only 9, 12, 30, 33, 51 and 54. These four modes require fewer than six of
  the active player's gangs in the sector, and modes 12 and 13 also leave out
  sectors the active player already owns. Inside the mode case, a sector owned
  by a hostile human gets +5; any other admitted sector gets +1.
- Mode 16 scores the sector returned by selector `0x77` +1.
- A mode above `0x3F` scores sector `mode - 0x40` +1.

After the mode switch, the common block at `0x004098D4..0x004099AE` multiplies
the score of each admitted sector owned by a hostile human by five. For modes
12 to 15 the instructions at `0x004093D3..0x004099AE` show that a hostile
human objective ends at 25.

Selector `0x77`, at `0x004045BB..0x0040465C`, scans all 81 planning slots in
ascending order without testing whether the gang is active, counts only the
records whose family byte is 11, treats ordinals 0, 6, 12 and so on as block
leaders, and on reaching the acting slot returns the first auxiliary value of
the most recent leader. Selector `0x76`, at `0x00404539..0x004045B7`, returns
1 only when the acting slot is itself a leader.

After scoring, the loop at `0x004099F8..0x00409B6D` first clears every sector
whose byte at sector record +15 is nonzero. It then reads the acting planning
record's family byte: only families 0 and 1 call selector `0x2C`, and for them
a destination not owned by the active player is cleared when the gang cannot
take it by Control on its own.

## Interpretation

Mode 10 heads for human land, or for any rival land when only computers play.
Modes 12 to 15 are the Big Man centre and the Eliminate headquarters. Mode 16
follows the family-11 block leader. The late filters drop sectors that are
unavailable, and for the general families 0 and 1 also enemy or neutral
sectors they could not take.

## Alternatives

The byte at sector record +15 is the Crackdown byte in FMT-STATE-002
(`crackdown_turns`), so "unavailable" reads as "under a Crackdown"; the
older notes call it an unavailable byte and do not settle it. Whether the
"multiplies by five" step in the common block applies to all modes or only to
sectors the mode admitted is read as all admitted sectors.

## How to reproduce

The address ranges above are in `0x00408642` and in the selector switch of
`0x00402D70`. The objective lists are compared as constants in the mode 12 to
15 cases; the common multiply by 5 follows the switch; the late filter loop
reads the planning record's byte 0 and compares it with 0 and 1.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
