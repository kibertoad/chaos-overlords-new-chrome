---
id: SCR-EVENT-001
title: Last Turn Events panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EVENT-001, FND-EVENT-002, FND-EVENT-003, FND-EVENT-005, FND-UI-016, FND-UI-012, FND-UI-001, FND-AUDIO-002, FND-AUDIO-011, FND-COMLINK-002, FND-COMLINK-007, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EVENT-005]
---

## Drawn elements

Page `p` shows record `p` of the active player's `last_turn_reports`
(FMT-STATE-006), whose fields are written `type`, `a1`, `a2` and `a3` here.
Text is drawn with the 6-by-7 character cells of `DATA/PX16/PX00129`
(FND-COMLINK-007).

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05010` | None | (104, 124, 344, 209) | While the panel is open | FND-EVENT-002, FND-EVENT-005 |
| Page number | Digits of `DATA/PX16/PX00129` | `events_page + 1`, two digits with a leading zero | (138, 137) | Always | FND-EVENT-005 |
| Page count | Digits of `DATA/PX16/PX00129` | The number of occupied records, two digits with a leading zero | (174, 137) | Always | FND-EVENT-005 |
| Previous | `DATA/PX16/PX00129` rectangle (118, 363, 26, 23), or (170, 363, 26, 23) on the first page | None | (135, 157, 26, 23) | Always | FND-EVENT-005 |
| Next | `DATA/PX16/PX00129` rectangle (144, 363, 26, 23), or (196, 363, 26, 23) on the last page | None | (163, 157, 26, 23) | Always | FND-EVENT-005 |
| Previous pressed | `DATA/PX16/PX00129` rectangle (66, 363, 26, 23) | None | (135, 157, 26, 23) | While Previous is held with the pointer inside it, or briefly after Left | FND-EVENT-005 |
| Next pressed | `DATA/PX16/PX00129` rectangle (92, 363, 26, 23) | None | (163, 157, 26, 23) | While Next is held with the pointer inside it, or briefly after Right | FND-EVENT-005 |
| Exit pressed | `DATA/PX16/PX00129` rectangle (50, 386, 50, 23) | None | (137, 293, 50, 23) | While Exit is held with the pointer inside it | FND-EVENT-005, FND-COMLINK-007 |
| Illustration | `DATA/PX16/PX06001` to `DATA/PX16/PX06009`: resource number 6000 plus `type`, for types 1 to 3 and 6 to 9 (type 0 would ask for resource 6000) | None | (198, 135, 242, 158) | `type` is not 4 or 5 | FND-EVENT-003, FND-EVENT-005 |
| Site picture | The rectangle (12, row + 1, 94, 62) of `DATA/PX16/PX02000`, where row is the top of the completed site's 64-pixel row, stretched to 242 by 158 by `StretchBlt` in `COLORONCOLOR` mode, then combined with the 8-by-8 pattern `Chaos Overlords.exe#BITMAP/146`, which keeps the pixels at x 0 and 4 of even rows and x 2 and 6 of odd rows and turns the other 75 percent black | None | (198, 135, 242, 158) | `type` is 4 | FND-UI-016, FND-EVENT-005 |
| Site overlay | `DATA/PX16/PX06004`, drawn after the pattern with the transparent copy, so the pattern does not touch it | None | (198, 135, 242, 158) | `type` is 4 | FND-UI-016, FND-EVENT-005 |
| Research monitor | `DATA/PX16/PX06005` | None | (198, 135, 242, 158) | `type` is 5 | FND-UI-012, FND-EVENT-005 |
| Researched item | One 48-by-48 frame of the item's 720-by-48 `DATA/PX16/PX04xxx` strip, copied as it is | Frame 0 when the page is drawn, then the animation under Timing | (296, 190, 48, 48) | `type` is 5 | FND-UI-012, FND-EVENT-005 |
| Eliminated player's portrait | `DATA/PX16/PX00129` rectangle (`portrait * 32`, 480, 32, 32) for player `a1`, stretched | None | (240, 237, 48, 48) | `type` is 9 | FND-EVENT-005 |
| Footer backing | Black fills | None | (298, 298, 138, 7) and (226, 307, 210, 7) | Always | FND-EVENT-005 |
| Year | Digits of `DATA/PX16/PX00129` | `(elapsed_turns - 1) / 52 + 2050`, four digits | (226, 298) | `elapsed_turns` is above 0 | FND-EVENT-005 |
| Week | Digits of `DATA/PX16/PX00129` | `(elapsed_turns - 1) % 52 + 1`, two digits with a leading zero | (256, 298) | `elapsed_turns` is above 0 | FND-EVENT-005 |
| Subject | Text | By `type` (table below) | (298, 298) | Every `type` except 0, and type 6 with `a1` other than 1, 2 or 4 | FND-EVENT-005 |
| Separator | `:` | None | (310, 298) | `type` is 4, or 6 with `a1` 2 | FND-EVENT-005 |
| Second name | Text | Site name for type 4, gang definition name cut to 20 characters for type 6 with `a1` 2 | (316, 298) | As Separator | FND-EVENT-005 |
| Caption | `Chaos Overlords.exe#STRING/33` to `Chaos Overlords.exe#STRING/44` (table below) | None | (226, 307) | Every `type` except 6 with `a1` other than 1, 2 or 4 | FND-EVENT-005 |

The caption string and the subject by `type` and `a1`:

- `type` 0: string 33, no subject.
- `type` 1, 2 and 3: strings 34, 35 and 36; the sector label of `a1`.
- `type` 4: string 37; the sector label of `a1`, the separator and the name
  of the site definition in slot `a2` of that sector.
- `type` 5: string 38; the name of item `a1`.
- `type` 6 with `a1` 1: string 39; the sector label of `a2`.
- `type` 6 with `a1` 2: string 40; the sector label of `a2`, the separator and
  the name of gang definition `a3`.
- `type` 6 with `a1` 4: string 41; the name of gang definition `a2`.
- `type` 7: string 42; the sector label of `a1`.
- `type` 8: string 43; the name of gang definition `a1`.
- `type` 9: string 44; the name in `player_names` of player `a1`.

A sector label is the letter `A` plus `sector % 8` followed by the digit `1`
plus `sector / 8`.

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Outside the panel | Anything outside (104, 124, 344, 209) | Always | A press or double-click plays the rejected-input sound | FND-EVENT-005 |
| Previous | (135, 157, 26, 23) | Always | Plays the accepted-input sound and holds the pressed face; on release inside, shows the previous report. On the first report, changes nothing and plays the rejected-input sound instead | FND-EVENT-002, FND-EVENT-005 |
| Next | (163, 157, 26, 23) | Always | As Previous, towards the next report; on the last report, rejected | FND-EVENT-002, FND-EVENT-005 |
| Exit | (137, 293, 49, 22) | Always | Closes the panel on release inside. No report is deleted | FND-EVENT-002, FND-COMLINK-002 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Left | Always | As Previous, with the pressed face shown for a short wait | FND-EVENT-002, FND-EVENT-005, FND-AUDIO-011 |
| Right | Always | As Next, with the pressed face shown for a short wait | FND-EVENT-002, FND-EVENT-005, FND-AUDIO-011 |
| Enter | Always | Presses Exit | FND-EVENT-002 |
| Execute (virtual key `0x2B`) | Always | Presses Exit | FND-EVENT-002 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` | Previous or Next is pressed where a step is allowed, before the pressed face is drawn; Exit is pressed | FND-EVENT-002, FND-EVENT-005, FND-AUDIO-011 |
| Rejected input | `DATA/SND00204` | Previous on the first report or Next on the last; a press outside the panel; the Events control is pressed with no report, and the panel then does not open | FND-EVENT-002, FND-EVENT-005, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Showing report `events_page` | RULE-EVENT-005 opens the panel, at page 0 at the start of a planning visit and at the page last shown when the Events control reopens it, or a legal step | Another step, or the panel closes | FND-EVENT-001, FND-EVENT-002, FND-EVENT-005 |
| Closed | Exit is released inside itself, or Enter or Execute is pressed | The Events control is pressed with at least one report, or a planning visit starts with at least one report | FND-EVENT-002, FND-EVENT-005, SRC-MANUAL-GOG |

## Timing

On a report of type 5 the researched item animates. Each timer-0 event the
panel consumes (six per second, FND-UI-001) advances a frame counter through
0 to 14 and back to 0, and copies frame `n`, the rectangle
(48 * n, 0, 48, 48) of the strip, to (296, 190). The counter restarts at 0
when the panel opens and after each page change [FND-EVENT-005].

## Differences between builds

None known.

## Open questions

- Resources are given from `DATA/PX16/`; at 8-bit colour depth the game loads
  the file of the same name from `DATA/PX08/`.
- How long the pressed arrow stays drawn after Left or Right depends on
  `fn_00464CD9(1)`, which was not read.
