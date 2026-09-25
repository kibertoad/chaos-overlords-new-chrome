---
id: FND-AI-030
title: The family-0 handler is a general state machine over the previous action
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00428EF0
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 0's handler `0x00428EF0` switches on the previous action (selector
`0x3E`) and ends with a comparison against the older action (selector `0x3F`).

- Previous 0 (None): when Force is below 8 and effective Heal is at least -3
  it writes Heal. Otherwise selector `0x5B` counts previous Hide assignments
  in the gang's sector: 0 writes Hide (8), a positive count writes Move
  through mode 5.
- Previous 1 (Attack): Move at once unless the cached opponent weight is 10.
  At weight 10 it makes one target draw (the human-pool and full-pool pair
  described in FND-AI-033) and uses selector `0x2B` for the comparison; then
  Attack on success, otherwise Control when the gang can take the sector on its
  own (selector `0x2C`), otherwise Move.
- Previous 8 (Hide) or 5 (Equip): at weight 10, up to five target draws, then
  Attack on the last drawn target even if every comparison failed. When no
  Attack was written, a positive selector `0x6C` enables a weapon and then an
  armor Equip with cooldown at most 0 and a new cooldown of the cost times 3;
  after that, in an owned sector Heal or Hide, and in another sector Move
  through mode 5.
- Previous 4 (Control): Hide in an owned sector, Move elsewhere.
- Previous 7 (Heal), 13 (Snitch) or 10 (Move): first the same Heal gate. At
  weight 10 exactly one target draw: success writes Attack, failure writes
  None and sets both auxiliary values to -1. Without weight 10, Control where
  the gang can take the sector alone, otherwise Hide or Move by the selector
  `0x5B` count.
- Previous 11 (Research): Move.
- Other previous actions keep None.

Finally, when the new action is Move and the older action is Move, the family
byte becomes 11 in scenario 7 and 2 in every other scenario.

## Interpretation

Family 0 is the default family: it heals, hides, probes for weak visible
enemies, and wanders through mode 5, and after two moves in a row it becomes a
formation gang (Siege) or an aggressive gang (elsewhere).

## Alternatives

Selector `0x5B` is shown elsewhere to count previous Chaos, not Hide
(FND-AI-019, FND-AI-037). Either the selector takes the action to count as an
argument, or this description of family 0 is wrong about which action it
counts. The owned-sector choice between Heal and Hide after Equip is not
written out.

## How to reproduce

Open `0x00428EF0`; the switch on selector `0x3E`, the mode 5 calls to
`0x00408642` (eight of them, FND-AI-028), and the final selector `0x3F`
comparison with 10 and the family store.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
