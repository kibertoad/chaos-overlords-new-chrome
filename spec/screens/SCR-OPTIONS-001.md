---
id: SCR-OPTIONS-001
title: Idle gang warning panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-OPTIONS-002, FND-UI-011, FND-UI-024, FND-AUDIO-011, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-OPTIONS-003, RULE-UI-003, SCR-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Warning panel with its wording, Cancel above OK | `DATA/PX16/PX05020` | None | `(104,124,344,209)` once slid in | Always | FND-OPTIONS-002 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Cancel | `(137,261,49,22)` | While open | Closes the panel; RULE-OPTIONS-003 returns 0 and planning goes on | FND-OPTIONS-002 |
| OK | `(137,293,49,22)` | While open | Closes the panel; RULE-OPTIONS-003 returns 1 and the turn ends | FND-OPTIONS-002 |
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

## Differences between builds

None known.

## Open questions

None.
