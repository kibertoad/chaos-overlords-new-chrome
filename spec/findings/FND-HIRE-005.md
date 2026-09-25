---
id: FND-HIRE-005
title: A per-player name modifier set at setup makes every hire by that player start at Force 10 without a draw
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E23F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475AAA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463B6C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464071
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A5EF0..0x004A5EF6
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The six bytes at `0x004A5EF0` hold one flag per player slot.
- Fresh local-game setup clears each byte, then, from `0x0046E23F` to
  `0x0046E272`, compares that player's length-prefixed name with the exact
  upper-case string `SMGMILK` and sets the player's byte on a match.
- The save reader at `0x00463B6C` and the save writer at `0x00464071`
  transfer all six bytes. Nothing resets them during play or after a hire.
- In the hire block of `0x00472775`, the test at `0x00475AAA` gives a flagged
  player's new gang Force 10 and skips the bounded Force draw.
- The same name scan sets other flags for other names next to this one.

## Interpretation

A player who enters that exact name at setup has every hire start at Force
10, and those hires make no random draw for Force. The flag lasts for the
whole match and is saved with it. It is a name-triggered modifier, not a
scenario or controller rule.

## Alternatives

This finding was recorded as part of FND-HIRE-001 before it was split. Names
that differ only in case do not match, since the comparison is with the exact
upper-case string; whether the setup screen forces names to upper case has not
been checked here.

## How to reproduce

Find the reference to the string `SMGMILK` in the setup code; it leads to the
compare at `0x0046E23F`. The flag array at `0x004A5EF0` is then read at
`0x00475AAA` in `0x00472775`, and transferred by the save routines at
`0x00463B6C` and `0x00464071`.
