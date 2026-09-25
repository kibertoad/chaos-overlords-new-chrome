---
id: FND-AI-047
title: The resolver lowers the target's attitude toward its attacker after every attack, evaded or not, and the loser's toward the winner of a Control takeover
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047278C..0x00472808
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00473EFC..0x00473FB8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475740..0x004757BA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB650..0x004AB667
tool: Ghidra 12.1.3
environment: null
---

## Observation

The attitude matrix is the `INT32` array at `0x004AB590`, cell `observer * 6 +
other` (FND-AI-006). The reaction values are the `INT32` array at
`0x004AB650`, one per player slot, written only by new-match setup
(FND-SETUP-015). All addresses below are in `fn_00472775` (range in
FND-EXE-004), and these are its only writes to the matrix.

- Recovery, `0x0047278C..0x00472808`: when the Mentality byte `0x00487850` is
  not 3, every cell below 10 is raised by one (the increment at
  `0x0047280B`). This runs first in the resolver, before any action.
- After an attack, `0x00473EFC..0x00473FB8`, at the end of the attack block's
  body (FND-COMBAT-008), reached by every gang whose action is Attack whether
  the attack was evaded, hit, or drew a retaliation. It loads the reaction of
  the player in the attacker's `target` byte (`0x00473EFC`), replaces it with
  the attacker's stored opening damage when that is larger
  (`0x00473F2E..0x00473F53`), subtracts the result from the cell
  `target_player * 6 + attacker_player` (`0x00473F7D`), and sets the cell to
  -10 when it is below -10 (`0x00473F84..0x00473FB8`). An evaded attack has
  opening damage -1, so the reaction is subtracted. The retaliation writes no
  cell.
- At a Control takeover, `0x00475740..0x004757BA`, reached only when the
  winner of a sector differs from its owner. When the previous owner is not -1
  (`0x00475740`), after raising the winner's overthrow count (`0x00475753`),
  it doubles the previous owner's reaction (`0x00475762..0x00475769`),
  subtracts it from the cell `previous_owner * 6 + winner` (`0x00475781`) and
  clamps the cell at -10 (`0x0047579A..0x004757BA`). The owner byte is then
  written at `0x004757D4` (FND-CONTROL-003).

The other writers of a sector's owner byte (`0x004395D4` in `fn_00439563`,
the Crackdown neutralization at `0x00473735`, `0x00476828` in `fn_00476726`,
and `0x00476FD7` in `fn_00476F3B`) are not followed by any write to the
matrix.

## Interpretation

`grudge_after_attack` lowers the target's owner's attitude toward the
attacker's player by the larger of the target's owner's reaction and the
opening damage, once per Attack order, including an evaded attack and an
attack on the attacker's own gang (which lowers the player's cell toward
itself). `grudge_after_takeover` is applied only by Control, only when the
sector had an owner, by twice the loser's reaction. A sector lost to a
Crackdown or to elimination changes no attitude.

## Alternatives

None known.

## How to reproduce

List the references to `0x004AB590`; in `fn_00472775` they are the
increment at `0x0047280B`, the subtraction at `0x00473F7D` with its clamp
store at `0x00473FB8`, and the subtraction at `0x00475781` with its clamp
store at `0x004757BA`. Read the loads of `0x004AB650` just before each
subtraction.
