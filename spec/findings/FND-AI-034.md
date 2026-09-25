---
id: FND-AI-034
title: The family-5 handler has the family-3 shape with Support sites and mode 7
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043A1D0
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 5's handler `0x0043A1D0` has the same action switch and ending as family
3 (FND-AI-033), with Support in place of Cash and mode 7 in place of mode 8:

- previous None, Control, Equip or Heal: Heal when Force is below 8 and
  effective Heal is at least -3; otherwise the first strictly greatest positive
  Support among unfinished sites of an owned sector gets Influence; otherwise
  Control when the gang can take the sector alone; otherwise Move through mode
  7;
- previous Snitch: Move through mode 7;
- previous Influence: the same equipment opportunity, Heal gate, keeping of
  the previous site and rescan;
- previous Attack, Hide or Move: the same weight-10 single draw, human-only or
  full list, comparison through the full list, and Attack or None;
- the same three-Move family change (11 in scenario 7, 2 otherwise) and the
  same scenario-0 Terminate in the last three turns.

## Interpretation

Family 5 builds Support the way family 3 builds Cash. Mode 7 also avoids
sectors where another gang is already continuing an Influence (FND-AI-026).

## Alternatives

None known.

## How to reproduce

Open `0x0043A1D0`; its four mode 7 calls to `0x00408642` are listed in
FND-AI-028, and the site scan compares the Support field.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
