---
id: FND-AI-031
title: The family-4 handler hides, probes and moves through mode 2
status: superseded
builds: [BLD-GOG-EN-1.1]
superseded_by: [FND-AI-049]
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00401000
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 4's handler `0x00401000` switches on the previous action and uses mode 2
for all its routing.

- Previous 0 (None), 4 (Control) or 7 (Heal): when Force is below 8 and
  effective Heal is at least -3, Heal; otherwise selector `0x5B` chooses Hide
  at a count of 0 and Move otherwise.
- Previous 1 (Attack), 13 (Snitch) or 10 (Move): at weight 10, one target
  draw; selector `0x2B` success writes Attack, failure writes None and clears
  both auxiliary values. Without weight 10, an owned sector chooses Hide at a
  selector-`0x5B` count of 0 and Move otherwise; a sector not owned chooses
  Control only when the previous and older actions are both Move and the gang
  can take the sector alone, otherwise Move.
- Previous 8 (Hide) or 5 (Equip): at weight 10, up to five draws and an Attack
  on the last selection even after all failures. Without Attack, selector
  `0x6C` enables the same weapon-then-armor Equip with cooldowns of cost times
  3. The remaining owned-sector branch Hides when the selector-`0x5B` count is
  below 2 and Moves otherwise; a sector not owned Moves.

It has no family change and no scenario 0 override. No cell of the family table
(FND-AI-002) assigns family 4; a gang can keep family 4 only through a cell
that preserves the current family.

## Interpretation

Family 4 is a cautious family that hides in its own land and roams to owned
sectors. It only arises from a family byte that already held 4.

## Alternatives

As for family 0, selector `0x5B` is recorded elsewhere as counting previous
Chaos rather than previous Hide (FND-AI-019). How a gang first gets family 4 is
not recorded.

## How to reproduce

Open `0x00401000`; its four mode 2 calls to `0x00408642` are listed in
FND-AI-028.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
