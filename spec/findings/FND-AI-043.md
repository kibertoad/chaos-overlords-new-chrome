---
id: FND-AI-043
title: Family 9 is seeded only for a network player the computer takes over; the planning pass's own seeding stores past the flag array
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00459431..0x004594DF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040AB20..0x0040ABBF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462B27..0x00462BF5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482158..0x0048215D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482180..0x004821AF
tool: Ghidra 12.1.3
environment: null
---

## Observation

In `0x00458FA0`, the dispatch loop (`0x0045946E..0x004594E7`) writes 9 to the
family byte of every record whose gang sector is not 100 before calling the
dispatcher, whenever the byte at `0x00482158 + player` is nonzero
(`0x004594AF`, store at `0x004594C6`).

The references to `0x00482158` are:

- `0x0040AA94` in `0x0040AA65`: writes 0 for players 0 to 5 at the start of a
  new match (FND-AI-045).
- `0x0040AB36` in `0x0040AB20`: writes 1 for one player.
- `0x00459467` in `0x00458FA0`: writes 1 at `[EBP-0xC] + 0x00482158`.
- `0x004594AF`: the read above.
- The save and load functions `0x00463CC5` and `0x0046381A` pass the address
  to the file transfer.

The store at `0x00459467` sits after the duplicate-cleanup loop
(`0x00459377..0x0045942C`), which counts `[EBP-0xC]` from 0 to 64 over the
sectors. It runs when selector 2 (turns remaining) is 13 or less
(`CMP EAX,0xD` / `JG` at `0x00459441`) and selector `0x31` for the player is
nonzero. `[EBP-0xC]` is not reloaded between the loop and the store, so it
holds 64 and the byte written is `0x00482198`. The dispatch loop that follows
resets `[EBP-0xC]` to 0 and reads `0x00482158 + player`, which the store did
not touch.

`0x00482198` lies inside a table of 64-bit floats that starts at `0x00482180`
(the first entry is 1.0 and the next ones are just below 1.0). The table is
read only by `0x0045CF05`, which has no callers (FND-EXE-004).

Selector `0x31` (`0x004043AB`) takes the single player whose byte at
`0x004ABC08` is 0 (selector `0x2E`), or player 0 when there is not exactly one
such player, and returns 1 when the given player's 32-bit value at
`0x004A2790` is below one fifth of that player's (signed division by 5) and
the given player's byte at `0x004ABC08` is greater than 3.

`0x0040AB20` sets `0x00482108 + player` and `0x00482158 + player` to 1, resets
all 81 of the player's planning records through `0x00409DE1`, writes family 9
to each record whose gang sector is not 100, and calls `0x00458FA0` for the
player. Its only caller is `0x00462579` at `0x00462B97`, in a loop over the six
player slots that runs for a slot whose controller (`0x004AB638`) is 3 when
`0x004243DA` returns 0 for the slot's connection handle at `0x004A08D0`; the
same branch first sets the controller to 1 and the handle to -1.

## Interpretation

The byte at `0x00482158` makes every gang of a player a family-9 raider for
the rest of the match: the pass rewrites the family before each dispatch, so
no family table cell and no handler's change survives. The planning pass was
written to switch a computer player to family 9 in the last 13 turns when it
trails the leader badly (less than a fifth of the leader's scenario score, at
least four players ahead of it). The store uses the sector loop's counter
instead of the player, so it never sets the player's flag, and it overwrites
the low byte of an entry in a float table that no running code reads. The
late-match switch therefore never happens (BUG-AI-005).

The only way a player gets family 9 is `0x0040AB20`, which runs when a network
player's connection is lost and the computer takes the slot over: from then on
all of that player's gangs, including later hires, plan as family 9. The flag
is kept in saves and cleared only when a new match starts.

## Alternatives

`[EBP-0xC]` could have been reloaded by a path Ghidra does not show, for
example a jump into the store from elsewhere; the store has one entry path,
the fall-through from the selector `0x31` test, and the only jump to
`0x0045946E` comes from the two failed tests before it.

A newly hired gang of a taken-over player has byte +1 set, so the dispatcher
wipes it and assigns a family from the table (FND-AI-041) after the pass has
written 9; the new family is overwritten with 9 again at the next pass, so such
a gang plans one turn under its table family.

## How to reproduce

In `0x00458FA0`, read the instructions from `0x00459431` to `0x004594C6`: the
two selector calls, the store through `[EBP-0xC]`, and the loop that follows.
The loop above it initialises `[EBP-0xC]` at `0x00459377` and compares it with
`0x40` at `0x00459386`. List the references to `0x00482158` and
`0x00482180..0x004821AF`, and the callers of `0x0040AB20` and `0x0045CF05`.
