---
id: FND-AI-039
title: The family-13 and family-14 handlers move to and hold the Big Man centre or the Eliminate headquarters
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040ABC0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00466910
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040B87D..0x0040B9A7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004675C8..0x004676F2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00467700..0x004677E0
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 13 (`0x0040ABC0`) and family 14 (`0x00466910`) move to fixed objective
sets: scenario 8 uses modes 12 and 14 (sectors 27, 28, 35, 36), scenario 6 uses
modes 13 and 15 (sectors 9, 12, 30, 33, 51, 54). Family 13 uses the modes that
leave out objectives the player already owns (12, 13); family 14 the modes that
keep them (14, 15).

Selector `0x1F` returns 1 only when the gang stands on an objective: in
scenario 8 in sector 27, 28, 35 or 36, in scenario 6 in sector 9, 12, 30, 33,
51 or 54. The terminal blocks at `0x0040B87D..0x0040B9A7` and
`0x004675C8..0x004676F2` replace the planned action with a Move through the
family's objective mode whenever selector `0x1F` is not 1 and the handler has
not written Equip. The equipment blocks sit inside selector-`0x1F` branches,
so a gang off its objective cannot have written Equip.

Family 14 continues at `0x00467700..0x004677E0` when selector `0x1F` is 1 or
the new action is Equip: it changes the action to Heal and its family to 13
exactly when the previous action is Control, Force is below 10 and effective
Heal is at least -3. A second test of the previous action against Attack
follows and cannot change the result.

On an objective, both handlers:

- When the sector is owned by the acting player and the cached weight
  (selector `0xAF`, the per-player cache of selector `0x90`) is 0: Heal at
  Force below 10 and effective Heal at least -3, before the equipment and
  site branches.
- When the sector is not the acting player's (contested): selector 2 gives the
  turns remaining. Only an even value with a nonzero weight enters target
  selection; otherwise Control. Target selection makes up to three draws: in a
  sector owned by a hostile human with weight 10, from the visible human gangs
  there; otherwise from the visible gangs of the sector's owner. Selector
  `0x2B` compares with the same ordinal in the full visible list; a success
  only ends the loop early, and after three draws the last one is used. At
  Force 5 or more that gang is attacked; with no gang or lower Force, Heal at
  Force below 10 and effective Heal at least -3, otherwise Control.
- When the sector is owned and opponents are visible: the full visible list,
  without the parity gate. The loop starts from 0 rather than 2, so it makes up
  to five draws, then the same Force, Heal and Control choice.
- When the sector is owned, no opponent is visible and the Heal gate fails:
  selector `0x61`'s weapon, then selector `0x64`'s armor, each needing a
  cooldown at most 0, a different item, enough cash and a previous action other
  than Attack, and writing the literal cooldown 2. Then selector `0x75`: a
  researched miscellaneous item (type 4) within the gang's own Tech whose Chaos
  bonus is strictly greater than the current item's (item 0 is the empty
  baseline), with an inclusive cash test and no cooldown. With no affordable
  item, the first strictly greatest positive Support among the three sites
  with positive remaining Resistance gets Influence; otherwise None.

Neither handler writes Research.

## Interpretation

Families 13 and 14 are the objective families of Eliminate and Big Man: they
walk to the objective, fight for it on alternate turns, and dig in once they
hold it. Family 14 can also settle a sector it holds and become family 13.

## Alternatives

The owned-objective Heal branch was located only by decompiler line positions
in both handlers. The contested-branch pool "visible gangs of the sector's
owner" may be the gangs of every visible opponent; the older notes say the
owner's.

## How to reproduce

Open the two handlers; the terminal ranges and the continuation range above
are instruction ranges. Selector `0x1F`'s case in `0x00402D70` holds the two
constant sector lists.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
