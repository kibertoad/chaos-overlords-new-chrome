---
id: FND-AI-048
title: The family-0 handler, read from its jump table, groups previous Chaos with Equip and Hide with Heal and Move, and chooses the human pool by the sector owner's attitude
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00428EF0..0x0042A6D3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042A618..0x0042A649
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x00428EF0` (range in FND-EXE-004) reads the gang's sector (selector `0x5A`)
and previous action (selector `0x3E`), then jumps through the table at
`0x0042A618` indexed by the byte table at `0x0042A63C` for actions 0 to 13
(`0x0042A5FC..0x0042A611`). The byte table is
`00 01 08 02 03 04 08 05 05 08 06 08 08 07`, so the targets are:

| Previous action | Target | Behaviour |
|---|---|---|
| 0 None | `0x00428F37` | Heal gate, else Chaos or Move |
| 1 Attack | `0x004290FE` | one draw at weight 10 |
| 3 Chaos, 5 Equip | `0x004294DB` | five draws at weight 10, then equipment and a sector branch |
| 4 Control | `0x00429F43` | Chaos in an owned sector, else Move |
| 7 Heal, 8 Hide, 10 Move | `0x0042A073` | Heal gate, one draw at weight 10, else Control, Chaos or Move |
| 13 Snitch | `0x0042A53E` | Move |
| 2, 6, 9, 11, 12 | `0x0042A64A` | nothing |

In the branches:

- The Heal gate is selector `0x3C` (gang byte +3, Force) below 8 and selector
  `0x51` (gang byte +0x18, effective Heal) above -4; it writes Heal (7).
- "Chaos or Move" calls selector `0x5B` (FND-AI-046): a count of 0 writes
  Chaos (3), a positive count writes Move (10) with the destination from sector
  selector `0x00408642` mode 5.
- Every draw first tests the sector's cached weight with selector `0xAF`. The
  pool is chosen by reading selector `0x21` for the sector (its owner byte, or
  -2 when the sector's byte +0x0F is nonzero) and the 32-bit value at
  `0x004AB590 + player * 0x18 + owner * 4`: when that value is negative and the
  weight is 10 (tested again), the draw counts selector `0x28` and takes
  selector `0x29` (visible gangs of players whose controller is 0 or 3);
  otherwise it counts selector `0xAA` and takes selector `0x91` (visible gangs
  of every other player). The ordinal comes from `0x0045D227`. The owner value
  is used as an index without a range test (for example `0x0042913C`,
  `0x00429533`, `0x0042A124`).
- Selector `0x2B` makes the strength test with the same ordinal in the
  selector-`0x91` list (BUG-AI-003).

Previous Attack (`0x004290FE`): weight not 10 writes Move through mode 5. At
weight 10, one draw; a passed test writes Attack (1) with the target's player
and roster slot and stores the sector in the 16-bit value at +10 of the
auxiliary record (`0x004292F7`); a failed test writes Control (4) when selector
`0x2C` returns 1 for the gang and sector, and Move through mode 5 otherwise.

Previous Chaos or Equip (`0x004294DB`): at weight 10, up to five draws, each
re-reading the owner's attitude and the weight, stopping at the first passed
test; Attack is then written on the last target drawn whether or not a test
passed, with the sector at aux +10 (`0x004296F7`). Then, when selector `0x6C`
is positive and the planned action (selector `0x3D`) is not Attack, a weapon
Equip (5) is written when selector `0x61` gives an item of 0 or more, the
weapon cooldown (selector `0x65`) is below 1, the item differs from the
equipped weapon (selector `0x39`) and its cost (16-bit at item record +0x7E,
`0x004A5F86 + item * 0xA6`) is at most cash (selector 3); the cooldown at +12
of the planning record becomes the cost times 3. Otherwise the same test with
selectors `0x64`, `0x66` and `0x3A` writes an armor Equip and sets the cooldown
at +14. Then, when the planned action is neither Equip nor Attack
(`0x004299EE`): in a sector the player owns (selector `0x21` equal to the
player), the Heal gate writes Heal, and otherwise a second owner test
(`0x00429E64`) that is again true writes Chaos; the Control store behind it
(`0x00429E94`) needs an owner other than the player. In a sector the player
does not own, weight 10 leads to one draw (`0x00429A54`), and any other weight
writes Move through mode 5.

Previous Control (`0x00429F43`): Chaos when the player owns the sector, Move
through mode 5 otherwise.

Previous Heal, Hide or Move (`0x0042A073`): the Heal gate writes Heal. Else at
weight 10, one draw: a passed test writes Attack with the sector at aux +10, a
failed test writes action 0 and -1 in aux +10 and +12 (`0x0042A308..0x0042A369`).
Else, when the player does not own the sector and selector `0x2C` is nonzero,
Control (`0x0042A3BA`); otherwise selector `0x5B` below 1 writes Chaos and a
positive count Move through mode 5.

Previous Snitch (`0x0042A53E`): Move through mode 5.

Most branches also write -1 to aux +10; an Attack writes the sector there.
After the switch (`0x0042A656`), when the
planned action is Move and the older action (selector `0x3F`, byte +2) is Move,
the family byte becomes 11 in scenario 7 and 2 otherwise.

## Interpretation

This corrects FND-AI-030 in three places. The branch it gives for previous Hide
or Equip is taken after previous Chaos or Equip; the branch it gives for
previous Heal, Snitch or Move is taken after previous Heal, Hide or Move; and
the Move it gives for previous Research is taken after previous Snitch, while
previous Research plans nothing. Where FND-AI-030 says Hide the branches write
Chaos.

Family 0 raises Chaos where no gang of its player did so last turn, and wanders
otherwise. After previous Chaos or Equip in an owned sector it always either
heals or raises Chaos again. The pool of every draw is the human-only list when
the player's attitude toward the sector's owner is negative, as family 3 does
(FND-AI-033). For a neutral sector (owner -1) or a sector under police presence
(-2) the attitude read falls outside the player's row: for player 1 to 5 it is
the previous player's attitude toward player 5 or 4, and for player 0 it is the
dword at `0x004AB58C` or `0x004AB588`, the bytes of `modifier_visibility`,
which is never negative. The read for the player's own sector is the player's
attitude toward itself.

## Alternatives

The weight-10 draw in the owned-sector test after Chaos or Equip cannot be
reached, because a weight of 10 has already produced an Attack before it; it is
recorded for completeness.

## How to reproduce

In `0x00428EF0`, read the bounds test and indirect jump at `0x0042A5FC` and the
byte table at `0x0042A63C`. Each target writes its action with a byte store to
`0x0048A258`; the stores are listed above.
