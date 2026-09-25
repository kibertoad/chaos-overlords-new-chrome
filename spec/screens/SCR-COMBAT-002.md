---
id: SCR-COMBAT-002
title: Detailed Combat panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-AUDIO-002, FND-AUDIO-013, FND-COMBAT-005, FND-COMBAT-009, FND-COMBAT-010, FND-COMBAT-011, FND-COMBAT-013, FND-DATA-003, FND-EXE-004, FND-GFX-005, FND-UI-001, FND-UI-010]
conflicting: []
split_with: []
related: [RULE-COMBAT-004]
---

## Drawn elements

The panel is built in a back buffer whose rows 144 to 353 are copied to the
screen at `(104,124)` [FND-UI-001], so panel-local `(x, y)` is screen
`(104 + x, 124 + y)`. The left side is always the viewer's gang. The
presentation is `fn_0042E040` and each clip is played by `fn_00430C23`
(ranges in FND-EXE-004) [FND-COMBAT-010].

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05014` | None | `(104, 124, 344, 209)` | While the presentation runs | FND-UI-001 |
| Sector tile | The city map art | The sector of the current clip | `(135, 135, 54, 52)` | During each clip | FND-UI-001 |
| Sector code | Not recorded | The sector's code | Text at `(156, 190)` | During each clip | FND-UI-001 |
| Gang portraits | 64-by-64 cell of `DATA/PX16/PX03000` (surface 3), column `n % 10` and row `n / 10`, where `n` is the definition's portrait number; for the police, from resource 300 loaded into surface 7 | The viewer's gang on the left, the other gang on the right | `(254, 172, 64, 64)` and `(327, 172, 64, 64)` | During each clip | FND-UI-001, FND-UI-010, FND-COMBAT-010, FND-COMBAT-013 |
| Force tracks | `DATA/PX16/PX00129`: the 60-by-3 red track `(354,3)`, then `6 * value` pixels of the green strip `(354,0)` | Two tracks per gang from its copy of the first eight bytes of its `combat_records` entry: the upper shows `force_start`, the lower `force_shown` as RULE-COMBAT-004 lowers it. Each track has a light, a full and a dark row | Left gang `(256, 240, 60, 3)` and `(256, 247, 60, 3)`; right gang `(329, 240, 60, 3)` and `(329, 247, 60, 3)`, from buffer x 152 and 225 and buffer rows 260 and 267 | During each clip | FND-UI-001, FND-UI-010, FND-COMBAT-009, FND-COMBAT-010 |
| Equipment, left | The item's `PX04xxx` rotation strip, one 48 by 48 frame chosen by the item record's last word | The left gang's weapon, armor and miscellaneous item | `(204, 172, 48, 48)`, `(204, 221, 48, 48)`, `(204, 270, 48, 48)` | During each clip, for each equipped item | FND-AUDIO-013 |
| Equipment, right | As on the left | The right gang's items | `(393, 172, 48, 48)`, `(393, 221, 48, 48)`, `(393, 270, 48, 48)` | During each clip, for each equipped item | FND-AUDIO-013 |
| Attack strip | See below | Eight 64 by 64 frames of the attacker | `(254, 254, 64, 64)` in a clip the viewer's gang makes, `(327, 254, 64, 64)` in a mirrored clip | Ticks 3 to 10 of each clip | FND-AUDIO-013, FND-COMBAT-005, FND-UI-001 |
| Hit strip | See below | Eight 64 by 64 frames of the gang hit | The other aperture | Ticks 3 to 10 of each clip | FND-AUDIO-013, FND-COMBAT-005, FND-UI-001 |
| Force lost | None (drawn) | The part of each bar that changed in the clip, in white | Over the force tracks | Ticks 13 and 15; restored on 14 and 16 | FND-COMBAT-005, FND-UI-001 |

The strips of a clip the viewer's gang makes, attack strip first:

- equipped weapon: `DATA/PX16/PX070nn` and `DATA/PX16/PX071nn`, with `nn`
  the weapon record's `attack_animation` and `hit_animation` words
  [FND-COMBAT-010];
- unarmed, base Martial Arts 0 or less: `DATA/PX16/PX07000` and
  `DATA/PX16/PX07102`;
- unarmed, base Martial Arts above 0: `DATA/PX16/PX07001` and
  `DATA/PX16/PX07118`;
- unarmed, definition 63: attack strip `DATA/PX16/PX07002`
  [FND-COMBAT-010];
- any attack that does no damage: the hit strip becomes `DATA/PX16/PX07101`;
- the target evaded: `DATA/PX16/PX07027` and `DATA/PX16/PX07100`.

A mirrored clip, an attack on the viewer's gang, uses the same cases with the
strips `PX072nn` and `PX073nn`. A police attack uses `DATA/PX16/Px07228` with
`DATA/PX16/Px07320`, or `DATA/PX16/PX07301` when it does no damage
[FND-AUDIO-002, FND-COMBAT-005]. A police attack that finds no gang is never
shown.

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Exit | Local `(33, 169, 50, 23)`, tracked as screen `(137, 293)-(187, 316)`; acts on release over the face | During a clip | Ends the whole presentation: every remaining clip is skipped | FND-COMBAT-010 |
| Outside the panel | Anywhere outside screen `(104, 124)-(448, 333)` | During a clip | Plays the rejected sound | FND-COMBAT-010 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Escape (`0x1B`) | During a clip | Draws the pressed Exit face and ends the whole presentation | FND-COMBAT-010, FND-COMBAT-011 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Attack with an equipped weapon | `DATA/SND00500` plus the weapon's sound number from its `DATA/ITEMS` record | Once per clip, just before the timeline starts advancing frames | FND-AUDIO-013 |
| Unarmed attack | `DATA/SND00500`, or `DATA/SND00501` when the attacking gang's base Martial Arts is above 0 | As above | FND-AUDIO-013 |
| Police attack | `DATA/Snd00518` | As above, for a police clip | FND-AUDIO-013, FND-COMBAT-005 |

A retaliation plays no sound [FND-COMBAT-005]. An evaded attack loads no valid
sound [FND-AUDIO-002].

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Clip | RULE-COMBAT-004 emits `CombatClip` | Tick 22, or tick 16 when the clip's hold flag is cleared | FND-COMBAT-005, FND-UI-001 |
| Closed | The last clip ends, or the player presses Escape or the Exit face | None | FND-COMBAT-005, FND-COMBAT-010 |

## Timing

The presentation runs on timer slot 0, which the game sets up at 6 Hz with a
period of `1000 / 6` milliseconds, 166 in integer arithmetic [FND-UI-001]. In
each clip, ticks 3 to 10 show the eight frames (about 1.33 seconds), ticks 13
and 15 draw the lost Force in white and ticks 14 and 16 restore the bars, and
ticks 17 to 21 hold the result before tick 22 ends the clip. A clip whose hold
flag is cleared, the first of a pair of gangs attacking each other, ends at
tick 16, and the next clip starts at once [FND-COMBAT-005, FND-UI-001].

## Differences between builds

None known.

## Open questions

- The tracks are drawn at panel-local y 116 and 123 (FND-COMBAT-009,
  FND-COMBAT-010); the capture of FND-UI-010 measured y 114 and 121. Which is right depends on the
  capture's unrecorded settings.
- The sector code's font.
- A freeze of the original during this presentation has been reported but not
  reproduced (BUG-COMBAT-001).
- The panel exists as `DATA/PX08/PX05014` too, and each strip as a `PX08` file;
  which is drawn depends on the display mode.
