---
id: SCR-SEARCH-001
title: Search panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-SEARCH-001, FND-SEARCH-002, FND-AUDIO-002, FND-AUDIO-011]
conflicting: []
split_with: []
related: [RULE-SEARCH-001, RULE-SEARCH-002]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05024` | None | (104, 124, 344, 209) | While the panel is open | FND-SEARCH-001, FND-SEARCH-002 |
| Row `n`, for site definitions 0 to 21 | Not recorded; the handler also loads the 220-by-56 marker sheet `DATA/PX16/PX00150` | Whether `search_filters` selects definition `n` for the active player (RULE-SEARCH-001) | Inside the row's target below | While the panel is open; drawn again after ALL, NONE or a click on the row | FND-SEARCH-001, FND-SEARCH-002 |
| ALL pressed | Shared pressed-control sprite 4; source not recorded | None | Over ALL | While ALL is pressed | FND-SEARCH-002 |
| NONE pressed | Shared pressed-control sprite 5; source not recorded | None | Over NONE | While NONE is pressed | FND-SEARCH-002 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Panel | (104, 124, 344, 209) | Always | Pointer input outside is moved to the nearest point inside before the controls are tested | FND-SEARCH-002 |
| ALL | (137, 140, 49, 23) | Always | `search_set_all(1)` (RULE-SEARCH-001), then draws every row again | FND-SEARCH-001, FND-SEARCH-002 |
| NONE | (137, 172, 49, 23) | Always | `search_set_all(0)` (RULE-SEARCH-001), then draws every row again | FND-SEARCH-001, FND-SEARCH-002 |
| Done | (137, 293, 49, 22) | Always | On release inside, closes the panel; the city then draws its markers by RULE-SEARCH-002 | FND-SEARCH-002 |
| Row `n`, single click | (206 + 116 * (n / 11), 146 + 15 * (n % 11), 114, 15) | Always | `search_toggle(n)` (RULE-SEARCH-001) | FND-SEARCH-001, FND-SEARCH-002 |
| Row `n`, double-click | As above | Always | Opens the Site Information panel, `DATA/PX16/PX05002`, for site definition `n`, and returns to Search when it closes | FND-SEARCH-001, FND-SEARCH-002 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Enter | Always | Presses Done | FND-SEARCH-002 |
| Execute (virtual key `0x2B`) | Always | Presses Done | FND-SEARCH-002 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` | Done is pressed | FND-SEARCH-002, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | The Search part of the Ranking and Search control is pressed | Done is released inside itself, or Enter or Execute is pressed | FND-SEARCH-002 |
| Site Information over Search | A row is double-clicked | The Site Information panel closes | FND-SEARCH-001, FND-SEARCH-002 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- How a row is drawn (a check mark, the site's marker from `PX00150`, or
  both) and where inside its target, is not recorded.
- Whether ALL and NONE play the accepted-input sound, as Done does, is not
  recorded.
- The Search handler has a rejected-input branch (FND-AUDIO-002), but what
  triggers it is not recorded.
- The single click that begins a double-click presumably flips the row first;
  whether the double-click flips it back is not recorded.
- The Site Information panel belongs to the interface area; its screen entry
  is not yet cross-referenced here.
