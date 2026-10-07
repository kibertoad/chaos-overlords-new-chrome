---
id: FND-AI-080
title: The family-0 owned-sector tests after previous Control and after previous Heal, Hide or Move read the owner query, which gives -2 under police presence
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00428EF0..0x00428F1B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00429F43..0x00429FBA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042A378..0x0042A3CB
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the family-0 handler `0x00428EF0` (FND-AI-048), the prologue stores the
result of selector `0x5A` for the player and roster slot, the gang's sector, in
the local at `[EBP-4]` (`0x00428F0E..0x00428F1B`).

The previous-Control target `0x00429F43` calls the selector dispatcher
`0x00402D70` with selector `0x21`, the sector from `[EBP-4]` and two zero
arguments, and compares the result with the player argument `[EBP+8]`
(`0x00429F55`). When they are equal it writes Chaos (3) to the planned action
at `0x0048A258` and to the gang's `action` byte at `0x00498DAF`
(`0x00429F70`, `0x00429F8A`); when they differ it jumps to `0x00429FBA`, which
writes Move (10) and takes the destination from sector selector `0x00408642`
mode 5. No other owner read precedes either store.

In the previous Heal, Hide or Move target `0x0042A073`, after the Heal gate
and the weight-10 draw, the block at `0x0042A378` calls the same dispatcher
with selector `0x21` and the sector from `[EBP-4]`, and skips to `0x0042A40D`
when the result equals the player (`0x0042A38A..0x0042A393`). Otherwise it
calls selector `0x2C` with the player, the slot and the sector, and a nonzero
result writes Control (4) (`0x0042A3A1..0x0042A3CB`).

Selector `0x21` returns the sector's owner byte, or -2 when the sector's byte
+0x0F (`crackdown_turns`) is nonzero (FND-AI-048).

## Interpretation

Both tests that FND-AI-048 left open read the owner query, as the test after
previous Chaos or Equip does, and none reads the raw owner byte. After previous
Control, a gang in a sector its player owns while police are present there
reads -2 and moves on through mode 5 instead of raising Chaos. After previous
Heal, Hide or Move the owner query only decides whether selector `0x2C` is
asked, and that selector refuses a sector under police presence or owned by the
player itself (RULE-AI-004), so the result does not depend on which owner value
is read.

## Alternatives

None. The selector number and the sector argument are pushed as immediates and
the prologue's local; no path writes `[EBP-4]` between the prologue and these
tests.

## How to reproduce

In `0x00428EF0`, read the instructions from `0x00429F43` to `0x00429F58` and
from `0x0042A378` to `0x0042A3AB`, and the store of the selector-`0x5A` result
at `0x00428F18`.
