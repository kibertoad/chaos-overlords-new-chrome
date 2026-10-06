---
id: SCR-OPTIONS-001
title: Idle gang warning panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-OPTIONS-002, FND-UI-011, FND-UI-024, FND-AUDIO-011, FND-EXE-004, SRC-MANUAL-GOG, FND-UI-047, FND-UI-054, EXP-UI-012, FND-UI-062, FND-UI-067]
conflicting: []
split_with: []
related: [RULE-OPTIONS-003, RULE-UI-003, SCR-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Warning panel with its wording, Cancel above OK | `DATA/PX16/PX05020` | None | `(104,124,344,209)` once slid in | Always | FND-OPTIONS-002 |
| Warning line | The strip `(165,189)-(262,198)` of the panel, or a black fill | None | `(269,169)-(366,178)` | Shown from the open for six ticks of slot 0, then black for two, and so on | FND-UI-024, FND-UI-054 |
| Cancel pressed | `DATA/PX16/PX00129` rectangle (0, 409, 50, 23); plain face (50, 409, 50, 23) while the pointer is outside and after a release that leaves the panel open | None | (137, 261, 50, 23) | While the face is held with the pointer inside it | FND-UI-062, FND-UI-067 |
| OK pressed | `DATA/PX16/PX00129` rectangle (0, 386, 50, 23); plain face (50, 386, 50, 23) while the pointer is outside and after a release that leaves the panel open | None | (137, 293, 50, 23) | While the face is held with the pointer inside it | FND-UI-062, FND-UI-067 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Cancel | `(137,261,49,22)` | While open | Held through the held-button helper; a release inside it closes the panel, RULE-OPTIONS-003 returns 0 and planning goes on | FND-OPTIONS-002, FND-UI-047, FND-UI-067 |
| OK | `(137,293,49,22)` | While open | Held through the held-button helper; a release inside it closes the panel, RULE-OPTIONS-003 returns 1 and the turn ends | FND-OPTIONS-002, FND-UI-047, FND-UI-067 |
| Inside the panel, off both faces | The rest of `(104,124,344,209)` | While open | None | FND-UI-024 |
| Outside the panel | Outside `(104,124,344,209)` | While open | Refused; plays slot 4. A double click does nothing | FND-UI-024 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Enter | While open | As OK | FND-OPTIONS-002 |
| Escape | While open | As Cancel | FND-OPTIONS-002 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Keyboard | `VK_EXECUTE` (`0x2B`) | While open | As OK | FND-OPTIONS-002 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Slide in and out | `DATA/SND00200`, `DATA/SND00201` | With Slide Panels on (RULE-UI-003) | FND-OPTIONS-002, FND-UI-011 |
| Accepted | `DATA/SND00203` (slot 3) | Cancel or OK is accepted, through the shared accepted-control helpers | FND-AUDIO-011, FND-UI-024 |
| Refused | `DATA/SND00204` (slot 4) | A press outside the panel | FND-UI-024 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | Done is accepted with Warn if Idle Gangs on and an idle gang (RULE-OPTIONS-003) | Cancel, OK, Enter or Escape | FND-OPTIONS-002 |

## Timing

The slide takes about a quarter of a second (RULE-UI-003).

The warning line shows for six ticks of the presentation clock and is filled
black for two, counted from the open, which starts the line shown
(FND-UI-054). While Cancel or OK is held with the pointer, the count stops.
The pass that ends with the release takes one tick if any fell during the hold
and loses the others (FND-UI-047).

## Differences between builds

None known.

## Open questions

None.
