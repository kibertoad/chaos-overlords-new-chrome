---
id: SCR-ATTACK-001
title: Attack picker (Target Acquisition)
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-ATTACK-001, FND-ATTACK-002, FND-ATTACK-003, FND-ATTACK-004, FND-AUDIO-002, FND-AUDIO-011, FND-COMBAT-013, FND-DETECT-001, FND-EXE-004, FND-GFX-005, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-ATTACK-002]
---

## Drawn elements

Positions are panel-local. The panel's origin on the screen is `(104,124)`:
the picker subtracts it from the pointer before its rectangle tests, and its
marks are drawn at the matching screen positions [FND-ATTACK-002,
FND-ATTACK-003]. The handler is `fn_0043B290(player, slot)` (range in
FND-EXE-004).

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05003` | None | Origin `(104,124)` | While the picker is open | FND-ATTACK-001 |
| Acting gang | 64-by-64 cell of `DATA/PX16/PX03000` (surface 3), chosen by the definition's portrait number; a blank from surface 6 for definition -1 | The gang being ordered | Local `(26, 17, 64, 64)` | While the picker is open | FND-ATTACK-003, FND-COMBAT-013 |
| Acting gang's equipment | 20-by-20 icons of `DATA/PX16/PX04999` (surface 5 from x 120), row `20 * id` for the item's `id` | The weapon, armor and miscellaneous item, each when not -1 | Local `(26, 82, 20, 20)`, `(48, 82, 20, 20)` and `(70, 82, 20, 20)` | While the picker is open | FND-ATTACK-003 |
| Opponent portraits | Surface 6 rows 480 (enabled) and 594 (disabled), column by the opponent's colour byte | The five other players in ascending slot order, skipping the acting player, one per cell | Local `(98, 16 + 36 * n, 32, 32)` for `n` 0 to 4 | While the picker is open; a disabled opponent's cell does not react | FND-ATTACK-001 |
| Target cards | Not recorded | The selected opponent's targetable gangs, from RULE-ATTACK-002 | Local target area `(135, 16, 202, 177)`, in six cells of RULE-ATTACK-002's list | After an opponent is selected | FND-ATTACK-001, FND-DETECT-001 |
| Opponent frame | `DATA/PX16/PX00129` crop `(120,171,34,34)`, keyed on exact white | Which opponent is chosen | Local `(97, 15 + 36 * n, 34, 34)`, one pixel outside portrait `n` | After an opponent is chosen | FND-ATTACK-002 |
| Target marker | `DATA/PX16/PX00129` crop `(66,299,48,48)`, keyed on exact white | Which target cell is chosen | Local `(143 + 68 * (c % 3), 18 + 90 * (c / 3), 48, 48)` for cell `c`, 8 pixels right of and 2 below the cell's corner | After a target is chosen | FND-ATTACK-002 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Opponent portrait `n` | Local `(98, 16 + 36 * n, 32, 32)`, `n` 0 to 4 | The opponent is enabled: the acting gang's sector record holds a nonzero `gangs_seen` byte for that player [FND-ATTACK-003] | Selects the opponent and clears the selected target; lists targets by RULE-ATTACK-002 | FND-ATTACK-001 |
| Target, top left | Local `(135, 16, 67, 89)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, top middle | Local `(202, 16, 68, 89)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, top right | Local `(270, 16, 67, 89)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, bottom left | Local `(135, 105, 67, 88)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, bottom middle | Local `(202, 105, 68, 88)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, bottom right | Local `(270, 105, 67, 88)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Confirm | Local `(33, 169, 50, 23)`, tracked as screen `(137, 293)-(187, 316)`; acts on release over the face | An opponent and a target are selected; otherwise plays the rejected sound | Writes the opponent's player slot into `target` and the chosen cell's roster slot into `target_2`, and closes the picker | FND-ATTACK-001, FND-ATTACK-003 |
| Cancel | Local `(33, 137, 50, 23)`, tracked as screen `(137, 261)-(187, 284)`; acts on release over the face | Always | Closes the picker without an order | FND-ATTACK-001, FND-ATTACK-003 |
| Outside the panel | Anywhere outside screen `(104, 124)-(448, 333)` | Always | Plays the rejected sound | FND-ATTACK-003 |
| Double-click, acting gang's portrait | Local `(26, 17, 64, 64)` | The acting gang's definition is not -1 | Opens the gang information panel for the acting gang, then redraws the markers and Confirm | FND-ATTACK-004 |
| Double-click, acting gang's item | Local `(26, 82, 20, 20)`, `(48, 82, 20, 20)`, `(70, 82, 20, 20)` | The item is not -1 | Opens Item Information for the weapon, armor or miscellaneous item | FND-ATTACK-004 |
| Double-click, target portrait `k` | Local `(136 + 68 * (k % 3), 17 + 90 * (k / 3), 64, 64)` | Cell `k` holds a target | Opens the gang information panel for that target | FND-ATTACK-004 |
| Double-click, target item | 20 by 20 at the cell's portrait corner plus `(0, 65)`, `(22, 65)` and `(44, 65)` | The target's item is not -1 | Opens Item Information for that item | FND-ATTACK-004 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Enter or plus (`0x0D`, `0x2B`) | Always; without an opponent and a target it plays the rejected sound | Confirm | FND-ATTACK-003 |
| Escape (`0x1B`) | Always | Draws the pressed Cancel face and closes without an order | FND-ATTACK-003 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` (effect slot 3) | An accepted confirm or cancel, through the shared panel helpers | FND-AUDIO-011 |
| Rejected input | `DATA/SND00204` (effect slot 4) | The handler refuses an input | FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| No opponent | The picker opens and no opponent is enabled | Never; only Cancel leaves | FND-ATTACK-001, FND-ATTACK-003 |
| Opponent chosen | The picker opens with an enabled opponent: the first enabled one, or the gang's current Attack target's player when the gang's action is already Attack and that player is enabled; or an enabled opponent is clicked | A target is clicked, or another opponent is clicked (which stays here with no target) | FND-ATTACK-001 |
| Target chosen | An enabled target is clicked, or the picker opens for a gang whose action is already Attack and the chosen opponent's list holds its `target_2` | Confirm, Cancel, or another opponent is clicked | FND-ATTACK-001 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The panel exists as `DATA/PX08/PX05003` too, and each image as a `PX08`
  file; which is drawn depends on the display mode, which the GFX entries
  describe.
