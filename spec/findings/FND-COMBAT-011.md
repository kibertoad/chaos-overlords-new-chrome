---
id: FND-COMBAT-011
title: Detailed Combat builds each focal gang's fight list from the combat result entries, resets displayed Force once for every listed gang, and keeps its state in two blocks of globals
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E040..0x0042EE45
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043087E..0x00430C22
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430C23..0x00431C53
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004745CE..0x00474733
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494578..0x004945CF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494710..0x004947C9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004947F8..0x004947FF
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges are those of FND-EXE-004. A gang's element number is
`player * 81 + roster_slot`.

The entries of `combat_results`. The resolver `fn_00472775` writes, for each
gang that fought, a four-byte entry of its sector's row at
`0x004A8888 + sector * 0x96 + player * 0x18 + k * 4`, `k` counting that
player's gangs in the sector in roster order: the first 16-bit value is the
gang's element number (`0x004745CE`), the second is its target's element
number `target * 81 + target_2` when the gang's `action` is 1, Attack
(`0x004746B1`), and -1 otherwise (`0x00474728`). The six bytes after the six
rows, at `0x004A8918 + sector * 0x96 + player`, are the per-player police
flags.

The presentation `fn_0042E040(viewer, automatic)`:

1. Sets the byte `0x00494760` to 1.
2. Lists the sectors, in ascending order, whose first entry for `viewer` is
   not -1 or whose police flag for `viewer` is set.
3. For every entry of every sector and every player, sets byte 3 of the
   entry's gang's combat record (`force_shown`) to byte 1 (`force_start`).
   This happens once, before any clip.
4. With no sector listed it plays effect slot 4 when `automatic` is 0, and
   returns. Otherwise it loads image 5014 into surface 7, slides the panel in
   with `fn_0041953E`, and for each listed sector draws the sector's map cell
   and name on the panel.
5. For each of `viewer`'s six entries in the sector whose gang is not -1 it
   stores the gang's element number in the INT16 at `0x004945A0`, the entry's
   target in the INT16 at `0x004947C8`, copies the gang's 10-byte combat
   record to `0x00494770`, and builds the fight list with `fn_0043087E`.
6. For each list element from 1 on, while `0x00494760` is still 1, it copies
   the element's 10-byte record to `0x00494578`, its element number to the
   INT16 at `0x00494584` and its target to the INT16 at `0x004945A4`, draws
   the two gangs with `fn_0042F98B`, and plays the clips below.
7. Slides the panel out with `fn_004196F5`.

The list builder `fn_0043087E(sector)` sets all 36 INT16 at `0x00494780` to -1
and fills three parallel lists: 10-byte records from `0x004945A8`, element
numbers from `0x00494780` and targets from `0x00494718`. Element 0 is the focal
gang. Element 1 is the focal gang's target when `0x004947C8` is not -1, with
the target's own target taken from its entry. Then come, in row order (player
0 to 5, entry 0 to 5), every entry of the sector whose target is the focal
gang and whose gang is not the focal gang's target. Last, when byte 9 of the
focal record (`police_damage`) is not -1, comes a police element: element
number -2, target the focal gang, and a record whose bytes 1 to 3 are 10, byte
4 is the focal gang's `police_damage` and the other bytes 0. The count goes to
the dword `0x00494710`.

For each list element the presentation plays:

- When the element is the focal gang's target: unless the focal record's
  `damage_dealt` (byte 4) is -1, the element's `force_shown` becomes
  `max(0, force_shown - damage_dealt)`; the focal gang's `force_shown` becomes
  `max(0, force_shown - retaliation_taken)` (byte 5). One clip is played with
  `fn_00430C23`, whose argument is true when the element's target is not the
  focal gang. Both records are written back to `combat_records`.
- When the element's target is the focal gang: the same with the roles
  exchanged, the element's `damage_dealt` against the focal gang and the
  element's `retaliation_taken` against itself, and a clip with argument true.
  The police element is not written back.

Before each clip the presentation sets the right end of the focal gang's bar
to `256 + 6 * force_shown` (INT16 at `0x0049476E`) and of the other gang's bar
to `329 + 6 * force_shown` (INT16 at `0x004947FE`). The clip player
`fn_00430C23` completes the two rectangles `(top, left, bottom, right)` at
`0x00494768` and `0x004947F8` with top 247, bottom 250 and the new left end,
and shrinks the bars toward it. It clears `0x00494760` when the player presses
Escape (virtual key `0x1B`) or releases the button at `(137,293)-(187,316)`;
every loop of the presentation then skips its remaining clips.

## Interpretation

The fight list comes from the combat result entries: the focal gang's own
attack, then the attacks the resolver recorded against it, then the police. A
gang whose action was not Attack has target -1 in its entry, so it appears
only as the target of someone else's entry. An evaded attack (`damage_dealt`
-1) takes nothing off the bar. Displayed Force is reset once for the whole
presentation, so a gang shown in two sectors' lists or in two focal gangs'
lists keeps the damage of the clips already shown.

The two blocks of globals are:

| Address | Type | Holds |
|---|---|---|
| `0x00494578` | FMT-STATE-003 | The other gang's combat record for the current clip |
| `0x00494584` | `INT16` | The other gang's element number, -2 for the police |
| `0x004945A0` | `INT16` | The focal gang's element number |
| `0x004945A4` | `INT16` | The other gang's target |
| `0x004945A8` | FMT-STATE-003 list | The fight list's records |
| `0x00494710` | `INT32` | The fight list's length |
| `0x00494718` | `INT16` list | The fight list's targets |
| `0x00494760` | `UINT8` | 1 while the presentation runs, 0 once the player ends it |
| `0x00494768` | `INT16[4]` | The focal gang's bar rectangle |
| `0x00494770` | FMT-STATE-003 | The focal gang's combat record |
| `0x00494780` | `INT16[36]` | The fight list's element numbers |
| `0x004947C8` | `INT16` | The focal gang's target, -1 for none |
| `0x004947F8` | `INT16[4]` | The other gang's bar rectangle |

## Alternatives

- The list's record area holds four elements before `0x004945D0`; a list of
  more elements writes past it. Whether a sector can produce more than four has
  not been checked.
- What `fn_0042F98B` and the clip player draw is left to the screen's
  findings.

## How to reproduce

In `0x0042E040`, find the store of 1 to `0x00494760`, the loop that copies
byte 1 of each record to byte 3, the loads from `0x004A8888` and `0x004A888A`,
and the calls to `0x0043087E`, `0x0042F98B` and `0x00430C23`. In `0x0043087E`,
find the stores of -1, the copies from `0x004A11E8` and the constants 10 and
-2. In `0x00472775`, find the stores at `0x004745CE`, `0x004746B1` and
`0x00474728`.
