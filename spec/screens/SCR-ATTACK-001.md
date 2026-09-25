---
id: SCR-ATTACK-001
title: Attack picker (Target Acquisition)
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-ATTACK-001, FND-AUDIO-002, FND-AUDIO-011, FND-DETECT-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-ATTACK-002]
---

## Drawn elements

Positions are panel-local: the panel's origin on the screen is not recorded
(see Open questions).

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05003` | None | Origin not recorded | While the picker is open | FND-ATTACK-001 |
| Opponent portraits | Not recorded | The five other players, one per cell | Local `(98, 16 + 36 * n, 32, 32)` for `n` 0 to 4 | While the picker is open; a disabled opponent's cell does not react | FND-ATTACK-001 |
| Target cards | Not recorded | The selected opponent's targetable gangs, from RULE-ATTACK-002 | Local target area `(135, 16, 202, 177)`, in six cells of RULE-ATTACK-002's list | After an opponent is selected | FND-ATTACK-001, FND-DETECT-001 |
| Target marker | Not recorded | None | On the right side of the chosen gang's card | After a target is chosen | SRC-MANUAL-GOG |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Opponent portrait `n` | Local `(98, 16 + 36 * n, 32, 32)`, `n` 0 to 4 | The opponent is enabled | Selects the opponent and clears the selected target; lists targets by RULE-ATTACK-002 | FND-ATTACK-001 |
| Target, top left | Local `(135, 16, 67, 89)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, top middle | Local `(202, 16, 68, 89)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, top right | Local `(270, 16, 67, 89)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, bottom left | Local `(135, 105, 67, 88)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, bottom middle | Local `(202, 105, 68, 88)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Target, bottom right | Local `(270, 105, 67, 88)` | The cell holds an enabled target | Selects that target | FND-ATTACK-001 |
| Confirm | Not recorded | An opponent and a target are selected | Writes the order as RULE-ATTACK-002 describes and closes the picker | FND-ATTACK-001 |
| Cancel | The common Cancel control; position not recorded | Always | Closes the picker without an order | FND-ATTACK-001 |

## Keyboard input

None known.

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
| No opponent | The picker opens | An enabled opponent is clicked | FND-ATTACK-001 |
| Opponent chosen | An enabled opponent is clicked | A target is clicked, or another opponent is clicked (which stays here with no target) | FND-ATTACK-001 |
| Target chosen | An enabled target is clicked | Confirm, Cancel, or another opponent is clicked | FND-ATTACK-001 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The panel's origin on the screen. The shared command panels are drawn at
  x 104, which would put the opponent cells at screen x 202, but no finding
  records the picker's origin.
- Which player each opponent cell shows (presumably the other five in slot
  order), and which cell each listed target takes.
- The acting gang's portrait, the selected gang's equipment and Force track,
  the Confirm control and the Cancel control: their positions and resources are
  not recorded in a finding.
- Keyboard input to the picker, and the exact conditions for the slot-4
  sound.
- The panel exists as `DATA/PX08/PX05003` too; which of the two files is drawn
  depends on the display mode, which the GFX entries describe.
