---
id: FND-COMBAT-010
title: Detailed Combat resets every listed gang's shown Force once, builds each focal list from the result rows, subtracts nothing for an evaded attack, and stops on Escape or the exit face
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
    address: 0x0042F779..0x0042F98A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043066C..0x0043087D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470278..0x0047029D
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function ranges are in FND-EXE-004. Result row entries are two 16-bit gang
indices, the gang and its Attack target or -1 (FND-COMBAT-008). Rectangles
below are written `(left, top)-(right, bottom)`; "buffer" means work surface
7, whose rows 144 to 353 are shown at screen `(104, 124)` (FND-UI-001), and
"local" means relative to the panel's top-left corner on screen.

Callers. The planning entry `fn_0046FD80` tests the byte `0x0048785C` at
`0x00470278`; when it is nonzero it calls `fn_0042E040(viewer, 1)` at
`0x00470288`, otherwise the Combat Results handler `fn_00451F80(viewer, 1)` at
`0x0047029D`. The byte is flipped by item 7 of menu `0x84`, Detailed Combat
(FND-UI-008), at `0x004627FC` in `fn_00462579`. The main console dispatcher
`fn_004718EE` calls `fn_0042E040(viewer, 0)` at `0x00471B0A` and
`fn_00451F80(viewer, 0)` at `0x00471B1F`, choosing by the pointer's position in
its control.

`fn_0042E040(viewer, flag)`:

- It sets the abort byte `0x00494760` to 1 (`0x0042E058`) and lists, in
  ascending order, the sectors where the first word of the viewer's first
  entry is not -1 or the viewer's police flag is set (`0x0042E05F..0x0042E0EC`).
- `0x0042E0F4..0x0042E1A5`, once, before anything is shown: for every sector,
  every player's row and every entry whose first word is not -1, it copies
  byte 1 of that gang's combat record into byte 3 (`0x0042E1A5`).
- With no listed sector it only calls `fn_00464290(4)` when `flag` is 0.
- For each listed sector, and each of the viewer's six entries in it
  (`0x0042E45B`) whose first word is not -1, the entry's gang is the focal gang
  and its second word the focal gang's target. It copies the focal record and
  calls the list builder `fn_0043087E(sector)` (`0x0042E550`).

`fn_0043087E` builds the list from the sector's rows only:

- entry 0 is the focal gang;
- when the focal gang's target is not -1 (`0x00430905`), the next entry is the
  target, with the target's own target taken from the target's entry in its
  player's row of the same sector (`0x00430945..0x004309C1`);
- then, over the six rows and six entries of the sector
  (`0x004309E5..0x00430B43`), every entry whose first word is not -1, whose
  second word is the focal gang, and whose first word is not the focal gang's
  target;
- last, when byte 9 of the focal record is not -1 (`0x00430B57`), a police
  entry with index -2, target the focal gang, bytes 1 to 3 set to 10 and byte 4
  set to the police damage.

For each listed entry after the focal gang (`0x0042E560`), while the abort
byte is 1:

- When the entry is the focal gang's target (`0x0042E69B`), it picks the strips
  and sound from the focal gang's weapon (item words at `0x9E`, `0xA0` and
  `0xA2`, read at `0x0042E749`, `0x0042E769` and `0x0042E729`), or, unarmed,
  from the base Martial Arts word of the focal gang's definition
  (`0x004A289A + definition * 0x9C`, `0x0042E6CE`), with attack strip 2 for
  definition 63 (`0x0042E711`). A damage byte of 0 selects hit strip 1, and -1
  selects attack strip 27, hit strip 0 and sound number -1
  (`0x0042E78D..0x0042E7C0`). Then, only when the focal gang's opening damage
  (record byte 4) is not -1, the entry's byte 3 is lowered by it, floored at 0
  (`0x0042E8C8..0x0042E8F7`); the focal gang's byte 3 is always lowered by its
  byte 5, floored at 0 (`0x0042E90A..0x0042E922`). The timeline is called with
  the hold flag set when the entry's target is not the focal gang
  (`0x0042E937`), and both records are written back (`0x0042E955..0x0042E99C`).
- When the entry's target is the focal gang (`0x0042E9AE`), a police entry
  loads `PX07301` when its damage is less than 1 and `PX07320` otherwise, with
  `Px07228` and sound 518 (`0x0042E9CC..0x0042ECE4`); another entry uses the
  mirrored strips chosen the same way from the entry's own record. The focal
  gang's byte 3 is lowered by the entry's byte 4 unless that is -1, floored at
  0 (`0x0042ED21..0x0042ED50`), the entry's byte 3 by the entry's byte 5,
  floored at 0 (`0x0042ED63..0x0042ED7B`), the timeline runs with the hold flag
  set (`0x0042ED82`), and the focal record and, for a gang entry only, the
  entry's record are written back (`0x0042ED99..0x0042EDF0`).

An entry that is both the focal gang's target and attacks the focal gang goes
through both branches, the first with the hold flag cleared.

The timeline `fn_00430C23(hold)` reads input once per pass
(`fn_00462579` at `0x00430DED`):

- key `0x1B` (Escape): draws the pressed look over screen
  `(137,293)-(187,316)`, clears the abort byte and ends the clip
  (`0x00430E3F..0x00430E9C`);
- a click inside screen `(104,124)-(448,333)` and inside local
  `(33,169)-(82,191)`: tracks the same face and, when released on it, clears
  the abort byte and ends the clip (`0x00430F1B..0x00430FBC`); a click outside
  the panel calls `fn_00464290(4)` (`0x00430FD6`);
- no other key is tested.

Force tracks. `fn_0042F779` draws the left gang's two tracks and
`fn_0043066C` the right gang's, each from surface 6, 6 pixels per point of
Force: the upper track at buffer row 260 shows byte 1 (`0x0042F785`,
`0x00430678`), the lower at buffer row 267 shows byte 3 (`0x0042F880`,
`0x00430773`), both 3 rows tall and starting at buffer x 152 for the left gang
and x 225 for the right. In the timeline, ticks 13 and 15 fill screen rows 247
to 249 of the lower track between the old and new values, ticks 14 and 16 copy
the lower tracks back from the buffer at screen `(256,247)` and `(329,247)`,
60 by 3, and tick 16 redraws both gangs' tracks. The clip ends at tick 16 when
the hold flag is clear and at tick 22 otherwise.

Portraits. `fn_0042F98B` copies a 64 by 64 cell of surface 3, column `n % 10`
and row `n / 10`, where `n` is the word at `0x004A281E + definition * 0x9C`
(`0x0042FC41`); for a police entry it loads resource 300 into surface 7
(`0x0043033A`) and copies from it.

## Interpretation

- Displayed Force is reset once for the whole presentation, for every gang in
  any row, so a gang shown in several focal lists keeps the damage already
  shown when it appears again.
- The list of gangs attacking the focal gang comes from the result rows, whose
  second word is -1 unless the gang's action was Attack; the action is
  therefore tested, at resolution time. Only gangs listed in the focal gang's
  sector are found, and the target is listed whatever its sector.
- An evaded attack lowers no bar of the target; the attacker's bar is lowered
  by its retaliation byte, which is 0 for an evaded attack.
- Escape or the exit face at local `(33,169)-(82,191)` stops the whole
  presentation, not just the clip.
- Each gang has two tracks, the Force before the phase above and the shown
  Force below, at local y 116 and 123 (screen 240 and 247).

## Alternatives

- The screen capture of FND-UI-010 measured the tracks at panel-local y 114
  and 121; this reading gives 116 and 123. The capture's measurement may
  include the bevel; the difference is not settled.
- Which resource surface 3 holds when Detailed Combat runs was not traced.

## How to reproduce

Read `fn_0046FD80` at `0x00470278`, then `fn_0042E040` from `0x0042E0F4`, the
list builder `fn_0043087E`, the two branches at `0x0042E69B` and `0x0042E9AE`,
and the event switch of `fn_00430C23` for the constants `0x1B`, `0xA9`,
`0x21`, `0xBF`, `0x52`. In `fn_0042F779` and `fn_0043066C`, read the loads of
`0x00494771`, `0x00494773`, `0x00494579` and `0x0049457B` and the rows `0x104`
and `0x10B`.
