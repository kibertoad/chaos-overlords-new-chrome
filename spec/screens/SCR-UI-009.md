---
id: SCR-UI-009
title: Application menu bar
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-008, FND-UI-007, FND-OPTIONS-001, FND-AUDIO-001, FND-AUDIO-002, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-UI-002, RULE-AUDIO-003, RULE-UI-003, RULE-OPTIONS-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Menu bar with File, Options, Comm and Help | `Chaos Overlords.exe#MENU/101`, drawn by Windows | Whether each Options item is checked, from `pref_thousands_colors`, `pref_full_screen`, `pref_base_stats`, `pref_detailed_combat`, `pref_slide_panels`, `pref_warn_idle`, and the checked level of `music_level` and `effects_level` | Above the 640-by-460 drawing area | Always, by the resource; the manual says it is hidden during play until the pointer reaches the top of the screen | FND-UI-008, SRC-MANUAL-GOG |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| File, New Game (`0x8101`) | Menu item | Not recorded | Leaves for new-game setup | FND-UI-008 |
| File, Open (`0x8102`) | Menu item | Not recorded | Loads a saved game | FND-UI-008 |
| File, Save (`0x8103`) | Menu item | Not recorded | Saves the game | FND-UI-008 |
| File, End (`0x8104`) | Menu item | Not recorded | Ends the game in progress | FND-UI-008 |
| File, Host (`0x8106`) and Join (`0x8107`) | Menu items | Not recorded | Start network play | FND-UI-008 |
| File, Exit (`0x8109`) | Menu item | Always | Quits | FND-UI-008 |
| Options, Thousands of Colors (`0x8401`) and Full Screen (`0x840B`) | Menu items | Not recorded | Toggle `pref_thousands_colors` and `pref_full_screen` | FND-UI-008, FND-OPTIONS-001 |
| Options, Music levels (`0x0601` to `0x060B`) | Menu items | Always | Set `music_level`, applied by RULE-AUDIO-003 | FND-UI-008, FND-AUDIO-001 |
| Options, Sound Effects levels (`0x0701` to `0x070B`) | Menu items | Always | Set `effects_level`, applied by RULE-AUDIO-003 | FND-UI-008, FND-AUDIO-002 |
| Options, Base Statistics (`0x8406`) | Menu item | Always | Toggles `pref_base_stats`: gang panels show base instead of current statistics | FND-UI-008, FND-OPTIONS-001 |
| Options, Detailed Combat (`0x8407`) | Menu item | Always | Toggles `pref_detailed_combat` | FND-UI-008, FND-OPTIONS-001 |
| Options, Slide Panels (`0x8408`) | Menu item | Always | Toggles `pref_slide_panels`, read by RULE-UI-003 | FND-UI-008, FND-OPTIONS-001 |
| Options, Warn if Idle Gangs (`0x8409`) | Menu item | Always | Toggles `pref_warn_idle`, read by RULE-OPTIONS-003 | FND-UI-008, FND-OPTIONS-001 |
| Comm, Disconnect (`0x8501`) and the transports None, WinSock, Modem and Direct Connect (`0x8503` to `0x8506`) | Menu items | Not recorded | Choose or drop the network transport, kept in `comm_type` | FND-UI-008, FND-OPTIONS-001 |
| Help, Help Topics (`0x8001`) | Menu item | Always | Opens the WinHelp file | FND-UI-008 |
| Help, About Chaos Overlords (`0x8003`) | Menu item | Always | Shows SCR-UI-002 | FND-UI-007, FND-UI-008 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Alt and the menu keys Windows provides | Always | Open the menus | FND-UI-008 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| None | | | | FND-UI-008 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| None | | | FND-UI-008 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Menu bar | The window is created | The game exits | FND-UI-008 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- Which items are disabled on which screens.
- Whether `0x0601` is level 0 or level 10, and the same for `0x0701`.
- The handlers of the File, Thousands of Colors, Full Screen and Comm commands
  have not been read; their effects are taken from their labels.
- Whether the menu bar hides during play, as the manual says, and how it is
  brought back.
- Whether an option change is written through RULE-OPTIONS-002 at once.
