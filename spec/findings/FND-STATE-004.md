---
id: FND-STATE-004
title: Per-player tables - the active flags at 0x004ABBE0, the length-prefixed names at 0x004A2588, signed research counters at 0x004A2608, and the standings bytes that AI selector 0x2D searches as if they were a player list
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBE0..0x004ABBE5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476F3B..0x00477129
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046EB91
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040F63D..0x0040F72D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2608..0x004A2787
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00403D0C..0x00403DA8
tool: Ghidra 12.1.3
environment: null
---

## Observation

FND-EXE-004 gives the ranges of the functions named here.

Active flags. The elimination helper `fn_00476F3B` ends by scanning each
player slot: when the slot owns no sector (byte 0 of every sector record
differs from it) and has no gang record whose sector byte differs from 100, it
stores 0 at `0x004ABBE0 + player` (`0x00477114`). The only other writer is the
match function `fn_0046E766`, which stores 1 for all six slots at `0x0046EB91`
in its new-match branch. The six bytes are save block 13 (FND-SAVE-001).
Readers test them for zero, among them the city map drawer (`0x0041246B`), the
end evaluator `fn_00476857` (`0x0047688A`), the scorer `fn_0047712A` and the
turn loop of `fn_0046E766` (`0x0046F37F`, `0x0046F3A6`, `0x0046F8ED`).
Before that scan, only in scenario 7, the same helper removes a player whose
Right Hands record (slot 0) has a nonzero definition byte or sector byte 100:
all its sectors become neutral with their site progress set to 0, and all 81
of its gang records get sector 100.

Names. The name entry routine `fn_0040F63D` copies the typed text from
`0x00498470` into the 12-byte record at `0x004A2588 + player * 12`:
characters go to bytes 1 onward (`0x0040F6E0`); any character below `0x20`
(space) or above `0x5A` (`Z`) is stored as a space (`0x0040F6C1`). The copy
stops after 10 characters or before a NUL. A NUL follows the last character
(`0x0040F70C`), and byte 0 receives the number of characters copied
(`0x0040F71D`). Readers load byte 0 with `MOVSX` and use it as a length, for
example to centre the name at `0x0040C886` (width `3 * length`), and pass the
address of byte 1 as the text (`0x0045EF39` and others). The 72 bytes are save
block 28.

Research counters. Every read of `0x004A2608` (element `item * 6 + player`)
uses `MOVSX`: the computer players' selector (`0x00404BBB` and six others),
`0x00437312`, `0x004379C9`, `0x0043F2BC`, the item-list builder `fn_004437E7`
(`0x00443A0C`, `0x00443A94`, `0x00443B41`, `0x00443B80`), the turn setup
(`0x0046EEF4`) and the Research case of the resolver (`0x00472EE4`). The
writers are the new-match setup (`0x0046DEEB`, `0x0046DF3C`) and the Research
case (`0x0047303A`).

Standings. The scorer `fn_0047712A` gives each active player's byte at
`0x004ABC08 + player` the number of players with a strictly greater score, and
each inactive player's byte `0xFF` (FND-AI-005). Selector `0x2D` of
`fn_00402D70`, given two player slots `p` and `q`, returns 0 when `q` is -1 or
equals `p`. Otherwise it finds the first index `i` in 0..5 whose byte at
`0x004ABC08 + i`, loaded with `MOVSX`, equals `p` (6 when none does), and the
first index `j` whose byte equals `q`, and returns 1 when `j < i` or `i == 0`,
else 0 (`0x00403D0C..0x00403DA8`). This selector is the only code that searches
`0x004ABC08` by value.

## Interpretation

`player_active` is the six bytes at `0x004ABBE0`: set for every slot when a
match starts, cleared when a player has neither a sector nor a living gang,
and saved with the match. A name record is a length byte, up to 10 characters
from space to `Z`, and a NUL. `research_remaining` is signed.

The six-byte table selector `0x2D` reads is the standings table, which holds
a place (0 = first) per player slot. The selector treats the places as if they
were player slots in ranking order, so what it compares is where the values
`p` and `q` first appear among the places, not the two players' standings.
It returns 1 whenever the value `p` sits at index 0, that is whenever player
slot 0 has place `p`.

## Alternatives

- Whether the name entry can receive lower-case letters at all depends on the
  dialog behind `0x00498470`, which has not been read; if it can, they are
  stored as spaces.
- The selector may have been meant to search a list of player slots sorted by
  standing. No such list exists in the executable: `0x004ABC08` is the only
  six-byte table it reads, and the turn order has no table (the passes count
  the slots from 0).

## How to reproduce

List the references to `0x004ABBE0`, `0x004A2588`, `0x004A2589`,
`0x004A2608` and `0x004ABC08`. Read `0x00476F3B..0x00477129`,
`0x0040F63D..0x0040F72D` and case `0x2D` of `0x00402D70`.
