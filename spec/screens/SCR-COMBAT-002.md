---
id: SCR-COMBAT-002
title: Detailed Combat panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-AUDIO-002, FND-AUDIO-013, FND-COMBAT-005, FND-COMBAT-009, FND-COMBAT-010, FND-COMBAT-011, FND-COMBAT-013, FND-COMBAT-014, FND-COMBAT-015, FND-COMBAT-016, FND-COMBAT-017, FND-DATA-003, FND-EXE-004, FND-GFX-005, FND-GFX-006, FND-UI-001, FND-UI-019, FND-UI-010, FND-UI-047, EXP-UI-019, EXP-UI-020]
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
| Sector tile | The sector's 54-by-52 cell of the map art on surface 2, at `(4 + 53 * column, 419 + 51 * row)`, without any owner's colour, under an unfilled black frame | The sector of the current clip | `(135, 135, 54, 52)` | During each clip | FND-UI-001, FND-COMBAT-017, EXP-UI-019 |
| Sector code | Not recorded | The sector's code | Text at `(156, 190)` | During each clip | FND-UI-001 |
| Header clearing | Black | None | The left side clears only its name field `(257, 138, 60, 8)`; the right side clears its whole header `(326, 135, 116, 36)` | During each clip | FND-COMBAT-014 |
| Colour strip | Fill in the owner's colour, the six-byte record at `0x004ABC18` | The owner of the gang on that side | `(205, 137, 18, 32)` and `(328, 137, 18, 32)` | During each clip, for a gang | FND-COMBAT-014 |
| Overlord portrait | `DATA/PX16/PX00129`, 32-by-32 cell `(32 * f, 480)` for the owner's portrait `f` | The owner of the gang on that side | `(223, 137, 32, 32)` and `(346, 137, 32, 32)` | During each clip, for a gang | FND-COMBAT-014 |
| Owner name | The font of `fn_00413FD5` (FND-UI-019) | The owner's name | Text at `(257, 138)` and `(380, 138)` | During each clip, for a gang | FND-COMBAT-014 |
| Police header | `DATA/PX16/Px00300` area `(208, 0, 116, 36)` | None | `(326, 135, 116, 36)` | During each clip with the police on the right | FND-COMBAT-014 |
| Gang portraits | 64-by-64 cell of `DATA/PX16/PX03000` (surface 3), column `n % 10` and row `n / 10`, where `n` is the definition's portrait number; for the police, the area `(0, 0, 64, 64)` of `DATA/PX16/Px00300`, resource 300, loaded into surface 7 | The viewer's gang on the left, the other gang or the police on the right | `(254, 172, 64, 64)` and `(327, 172, 64, 64)` | During each clip | FND-UI-001, FND-UI-010, FND-COMBAT-010, FND-COMBAT-013, FND-COMBAT-014 |
| Force tracks | `DATA/PX16/PX00129`: the 60-by-3 red track `(354,3)`, then `6 * value` pixels of the green strip `(354,0)` | Two tracks per gang from its copy of the first eight bytes of its `combat_records` entry: the upper shows `force_start`, the lower `force_shown` as RULE-COMBAT-004 lowers it. Each track has a light, a full and a dark row | Left gang `(256, 240, 60, 3)` and `(256, 247, 60, 3)`; right gang `(329, 240, 60, 3)` and `(329, 247, 60, 3)`, from buffer x 152 and 225 and buffer rows 260 and 267 | During each clip | FND-UI-001, FND-UI-010, FND-COMBAT-009, FND-COMBAT-010, FND-COMBAT-015, EXP-UI-019, EXP-UI-020 |
| Equipment, left | The item's `PX04xxx` rotation strip, one 48 by 48 frame chosen by the item record's last word; black for an empty slot | The left gang's weapon, armor and miscellaneous item | `(204, 172, 48, 48)`, `(204, 221, 48, 48)`, `(204, 270, 48, 48)` | During each clip | FND-AUDIO-013, FND-COMBAT-014 |
| Equipment, right | As on the left; for the police, the areas `(64, 0)`, `(112, 0)` and `(160, 0)` of `DATA/PX16/Px00300`, 48 by 48 | The right gang's items, or the police's three pictures | `(393, 172, 48, 48)`, `(393, 221, 48, 48)`, `(393, 270, 48, 48)` | During each clip | FND-AUDIO-013, FND-COMBAT-014 |
| Attack strip | See below | Eight 64 by 64 frames of the attacker | `(254, 254, 64, 64)` in a clip the viewer's gang makes, `(327, 254, 64, 64)` in a mirrored clip | Ticks 3 to 10 of each clip | FND-AUDIO-013, FND-COMBAT-005, FND-UI-001 |
| Hit strip | See below | Eight 64 by 64 frames of the gang hit | The other aperture | Ticks 3 to 10 of each clip | FND-AUDIO-013, FND-COMBAT-005, FND-UI-001 |
| Darkened frames | Black drawn through bitmap 143, the pattern the grey 0x7FFF selects, over the last frame of both strips | The last frames with every other pixel black, starting with black at each frame's top-left corner | Both apertures | From tick 12 until the clip ends | FND-COMBAT-014, FND-GFX-006 |
| Force lost | None (drawn) | The part of each bar that changed in the clip, in white | Over the force tracks | Ticks 13 and 15. Tick 14 copies back the lower tracks as painted before the clip's ticks, and tick 16 paints them with the lowered Force | FND-COMBAT-005, FND-UI-001, FND-COMBAT-016, EXP-UI-019, EXP-UI-020 |

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
each clip, ticks 3 to 10 show the eight frames (about 1.33 seconds), tick 12 darkens the
last frames [FND-COMBAT-014], ticks 13
and 15 draw the lost Force in white, tick 14 shows the lower tracks with the
Force before the clip again and tick 16 with the lowered Force, and ticks 17
to 21 hold the result before tick 22 ends the clip [FND-COMBAT-016]. The tick
is the local at `ebp - 0x20` of `fn_00430C23`; the screen shows the passes up
to that local less 1 [FND-COMBAT-016]. A clip whose hold
flag is cleared, the first of a pair of gangs attacking each other, ends at
tick 16, and the next clip starts at once [FND-COMBAT-005, FND-UI-001].

While the Exit face is held with the pointer, the clip stops. A release outside
the face lets it go on: that pass takes one tick if any fell during the hold
and loses the others [FND-UI-047].

## Differences between builds

None known.

## Open questions

- Which function draws the sector code. Its pixels are those of the captures
  of EXP-UI-019 and EXP-UI-020.
- No capture shows a clip of an attack on the viewer's gang, an evaded attack
  or the pressed Exit face.
- A freeze of the original during this presentation has been reported but not
  reproduced (BUG-COMBAT-001).
- The panel exists as `DATA/PX08/PX05014` too, and each strip as a `PX08` file;
  which is drawn depends on the display mode.
- No recorded run of the original reaches the strips of an equipped weapon, of
  Martial Arts above 0, of definition 63, of a hit with no damage, of an
  evaded attack and of every mirrored clip; ticks 3, 10 and 22 and a clip
  ended at tick 16; the 166 ms period as a measurement; an Exit hold pausing
  the clip; the pressed Exit face, the refused press outside and Escape; sound
  501 and the silent retaliation (FND-COMBAT-005, FND-COMBAT-010,
  FND-COMBAT-011, FND-COMBAT-014, FND-COMBAT-016, FND-UI-001, FND-UI-047,
  FND-AUDIO-002, FND-AUDIO-013). These rest on the static findings named, so
  the entry stays `supported` (DECISIONS.md, 2026-10-06).
