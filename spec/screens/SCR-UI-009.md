---
id: SCR-UI-009
title: Application menu bar
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-008, FND-UI-007, FND-OPTIONS-001, FND-AUDIO-001, FND-AUDIO-002, SRC-MANUAL-GOG, FND-UI-021, FND-UI-020, FND-HELP-005, FND-PLATFORM-009]
conflicting: []
split_with: []
related: [SCR-UI-002, RULE-AUDIO-003, RULE-UI-003, RULE-OPTIONS-003, RULE-HELP-001, RULE-UI-013, RULE-UI-014]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Menu bar with File, an empty greyed Edit item, Options, Comm and Help | `Chaos Overlords.exe#MENU/101`, drawn by Windows | Whether each Options item is checked, from `pref_thousands_colors`, `pref_full_screen`, `pref_base_stats`, `pref_detailed_combat`, `pref_slide_panels`, `pref_warn_idle`, the checked level of `music_level` and `effects_level`, and the checked transport of `comm_type`; greyed items as listed under Mouse input | Above the 640-by-460 drawing area, which starts at the client area's top-left corner | Always: the menu is attached for the whole run and never hidden, although the manual says it hides during play | FND-UI-008, FND-UI-021, SRC-MANUAL-GOG |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| File, New Game (`0x8101`) | Menu item | On the title; greyed while a game is in progress, a setup or network screen runs, the About screen shows or a panel is open | Leaves for new-game setup (RULE-UI-013) | FND-UI-008, FND-UI-021 |
| File, Open (`0x8102`) | Menu item | As New Game | Loads a saved game | FND-UI-008, FND-UI-021 |
| File, Save (`0x8103`) | Menu item | During planning when saving is allowed; greyed on the title, during resolution, while a panel is open and after a setup or network screen | Saves the game | FND-UI-008, FND-UI-021 |
| File, End (`0x8104`) | Menu item | During planning, unless a panel is open | Ends the game in progress, offering to save first | FND-UI-008, FND-UI-021 |
| File, Host (`0x8106`) and Join (`0x8107`) | Menu items | On the title when `comm_type` is not None | Start network play | FND-UI-008, FND-UI-021 |
| File, Exit (`0x8109`) | Menu item | Unless a panel is open or the About screen shows | Quits, offering to save first during a game | FND-UI-008, FND-UI-021 |
| Options, Thousands of Colors (`0x8401`) and Full Screen (`0x840B`) | Menu items | Full Screen always; Thousands of Colors only when both image sets are installed and the game runs full screen | Show dialog 136, then toggle `pref_thousands_colors` or `pref_full_screen`; the change takes effect at the next start | FND-UI-008, FND-OPTIONS-001, FND-UI-020, FND-UI-021 |
| Options, Music levels (`0x0601` to `0x060B`) | Menu items | Always | Set `music_level` to the item's ID minus `0x0601`, so `0x0601` is level 0, applied by RULE-AUDIO-003 | FND-UI-008, FND-AUDIO-001, FND-UI-020 |
| Options, Sound Effects levels (`0x0701` to `0x070B`) | Menu items | Always | Set `effects_level` to the item's ID minus `0x0701`, applied by RULE-AUDIO-003 | FND-UI-008, FND-AUDIO-002, FND-UI-020 |
| Options, Base Statistics (`0x8406`) | Menu item | Always | Toggles `pref_base_stats`: gang panels show base instead of current statistics | FND-UI-008, FND-OPTIONS-001 |
| Options, Detailed Combat (`0x8407`) | Menu item | Always | Toggles `pref_detailed_combat` | FND-UI-008, FND-OPTIONS-001 |
| Options, Slide Panels (`0x8408`) | Menu item | Always | Toggles `pref_slide_panels`, read by RULE-UI-003 | FND-UI-008, FND-OPTIONS-001 |
| Options, Warn if Idle Gangs (`0x8409`) | Menu item | Always | Toggles `pref_warn_idle`, read by RULE-OPTIONS-003 | FND-UI-008, FND-OPTIONS-001 |
| Comm, Disconnect (`0x8501`) and the transports None, WinSock, Modem and Direct Connect (`0x8503` to `0x8506`) | Menu items | The transports on the title until Host or Join is chosen; the whole Comm menu is greyed from New Game or Open until the game ends; Disconnect during planning of a network game | Disconnect leaves the network game; a transport sets `comm_type` to 0 to 3 | FND-UI-008, FND-OPTIONS-001, FND-UI-020, FND-UI-021 |
| Help, Help Topics (`0x8001`) | Menu item | Always, except on the About screen | None: RULE-HELP-001 | FND-UI-008, FND-HELP-005 |
| Help, About Chaos Overlords (`0x8003`) | Menu item | Always, except on the About screen | Shows SCR-UI-002 | FND-UI-007, FND-UI-008, FND-UI-021 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Alt and the menu keys Windows provides | Always | Open the menus | FND-UI-008 |
| Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+R, Ctrl+H, Ctrl+J | In loops that read events (RULE-UI-014) | Send New Game, Open, Save, End, Host and Join, the same events as the menu items; Backspace and Ctrl+Enter give the codes of Ctrl+H and Ctrl+J | FND-UI-021, FND-UI-020 |

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

- Whether Windows sends an accelerator's command when its item is greyed needs
  a run of the original.
