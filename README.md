# Chaos Overlords: New Chrome

A clean-room MonoGame reimplementation of the 1996 turn-based strategy game.
This repository intentionally contains **no original game assets**. You must own
a supported legal copy. The DRM-free
[GOG release of *Chaos Overlords*](https://www.gog.com/en/game/chaos_overlords)
is fully compatible with the new runtime as an asset source. The extractor
validates its asset pack (the executable is neither required nor copied) and
creates a local asset pack containing repaired graphics plus the original
audio, music, video, help, and currently opaque resources. Compact gameplay
tables are bundled in the open-source core; original art and media are not.

Original save import/export is not supported. Recreation-native saves and
replays are development formats until 1.0.0 and may change incompatibly before
then; the versioned migration machinery is retained for post-1.0 compatibility.

## Quick start

1. Buy and install a legal copy of
   [*Chaos Overlords* from GOG](https://www.gog.com/en/game/chaos_overlords).
2. Download and run the latest **Chaos Overlords: New Chrome** Windows installer
   from [GitHub Releases](https://github.com/kibertoad/chaos-overlords-new-chrome/releases/latest).
3. Let Setup detect your GOG installation, or select its folder when prompted.
   Setup verifies and imports the required assets, then installs the new runtime.

The installer contains no original assets and requires an installed legal copy
when importing them.

## Project status

New Chrome is a broad, playable pre-1.0 recreation. A complete match can be
played locally from setup through results, and the deterministic core, original
asset importer, modern online transport, native saves, and replays are all
operational. It is not yet a claim of pixel-perfect or rule-perfect parity with
the shipped 1996 executable. The detailed evidence and next proof gate for each
system are tracked in the [parity matrix](docs/PARITY-MATRIX.md).

### Implemented

| Area | Available now |
|---|---|
| Installation and assets | Windows, Linux, and macOS packaging can verify a supported legal GOG installation and import the required assets. The transactional extractor repairs RGB555 graphics, decodes indexed graphics and WinHelp content, imports audio/music/video, validates exact output inventories and hashes, and never copies the original executable. |
| New Game setup | All ten scenarios are mapped to their correct visual buttons and default to Greed. Every mode and game-length button has a rules tooltip. The four duration choices are active only for the timed Greed, Power, Acceptance, and Dominance modes; objective modes run until their goal is reached, hide the duration selection light, and explain why duration clicks are disabled. Setup supports the global AI Mentality, one-to-six named local humans, portrait/color rearrangement, six total participants after AI fill, and optional None/30-second/2-minute/5-minute planning clocks. |
| Local and hot-seat play | Deterministic turns run through Upkeep, Command, Execution, Hire, and Elimination. Multiple local humans receive the original private handoff screen. The optional idle-gang warning prevents accidental completion while an active gang has no order. |
| Commands and economy | All 14 original actions are validated, queued, cancelled, and resolved, with the recovered recurring subset enforced. Dragging a gang onto a neighboring sector queues Move, onto an eligible site queues Influence, and onto a visible enemy gang queues Attack. Hire, Reject, Equip, Give, multi-item Sell, Research, Bribe, Chaos, Control, Heal, Hide, Snitch, and Terminate are playable. Cash, upkeep, sector tax, influenced-site income, debt restrictions, site effects, delayed Influence activation, and the recovered Factory/Sell rules are represented. |
| City, sector, and management UI | The 640x460 interface includes the 8x8 city, 3x3 detailed-sector neighborhood, up to six gang cards, three equipped-item cells in Gang Information, site/item details, City and Sector Finance, Ranking, Hire comparison, Comlink, Game Info, Research/Equipment, Give/Sell, Combat Summary/Detail, Options, Help, and endgame panels. Mouse and keyboard input, right-click cancellation, nested-panel return, panel-motion control, and windowed/borderless-fullscreen presentation are wired. |
| Search and turn reports | Search: Sites exposes all 22 site types with ALL/NONE and individual filters. Controlled sites are always shown as white transparent markers; selected uncontrolled site types appear amber only while the overview is applied. Last Turn Events auto-opens when required, preserves unread progress until every page is viewed, remains reviewable afterward, and uses the recovered native site crop, palette stretch, ordered mask, foreground event art, and footer layout. Options can switch event-site backgrounds from Original to Smooth filtering. |
| Combat and police | Simultaneous combat, retaliation, Hide/evasion, casualties, equipment loss, Crackdowns, police detection and damage, and combat statistics are implemented. Summary results are grouped by sector; Detail replays the chosen fight with decoded eight-frame animations and event-time equipped, unarmed, retaliation, and police sounds. Detailed/Simple presentation changes no authoritative outcome. |
| Objectives, ranking, and AI | All ten scenarios have timed/objective completion, recovered score tables, competition ranking, tied winners, sole-survivor handling, elimination cleanup, awards/statistics, and Siege objective markers. Deterministic computer players use the recovered difficulty bands, attitudes/reactions, hiring and placement rules, and handlers for every known strategy family. |
| Saves and replays | Escape opens an in-game Resume/Save/Load/Quit-to-main-menu menu; quitting requires confirmation that unsaved progress will be lost. F5/F9 open a nine-slot save/load browser. Saves suggest an editable name and display timestamp, scenario, single/hot-seat/online type, and human/AI counts. Atomic writes, backups, autosaves, and corruption recovery are implemented. F6/F10 save and verify deterministic local replays. Current formats are save v22, replay v24, and canonical hash v25. |
| Online play | The title screen can host or join matches through the new self-hostable coordination service. It privately seals simultaneous order sets, distributes deterministic seeds and slot assignments, verifies client state hashes, resumes its ordered event stream, and supports host-snapshot desync recovery. See [Multiplayer](docs/MULTIPLAYER.md). This does not reproduce the original network protocols. |
| Help, audio, and options | F1 opens a cross-platform viewer for the imported original Help contents, styles, internal jumps, and definition popups, augmented with verified executable formulas. The recovered title/game/endgame music programs, independent 0-10 music/effect levels, focus pause/resume, mapped interface sounds, combat cues, planning warnings, display mode, gang-stat mode, combat detail, panel motion, idle warning, and event-image filter are implemented and persisted. |
| Engineering baseline | The authoritative simulation is headless and deterministic; saves, replays, online lockstep, and phase hashes share that state model. Automated coverage spans extraction, persistence/migrations, all command resolvers, scenarios, AI families, UI projections/layouts, networking contracts, installers, and multi-turn deterministic campaigns. Static-analysis findings, confidence, and unresolved behavior are documented rather than silently guessed. |

### Still missing or provisional

| Area | Remaining work |
|---|---|
| Exact gameplay parity | Capture native initial-city/setup/RNG fixtures and more controlled runtime traces. Confirm remaining action-order, takeover, debt, notification, targetability, transaction, repeat-command, and special-objective boundaries where current behavior is documented as provisional or supported only by static/manual evidence. |
| Original AI parity | Compare full native AI decisions and RNG consumption against fixed reference traces, expand multi-seed tournament coverage, and establish reliable evidence-led completion behavior for Kill 'Em All, Big 40, Eliminate, Siege, and Armageddon. Current AI is playable and deterministic, but complete outer-planner parity is not proven. |
| Visual and input parity | Finish original hit maps and golden-screen comparisons; validate remaining offsets, transparency/color keys, combat compositing, bare-hand style, Siege pylons, endgame/hot-seat sequencing, and panel animation cadence. Configurable key bindings and broader accessibility work are not implemented. |
| Online experience | There is no public lobby browser, spectator/join-in-progress flow, lobby chat, online Comlink integration, or automatic AI takeover for a departed player. A desync still depends on the host supplying a snapshot. Security and deployment limitations are documented in [Multiplayer](docs/MULTIPLAYER.md). |
| Media and platform polish | The two Smacker movies are extracted but not played. Some interface/impact sound triggers and exact menu music restart boundaries remain unmapped. Installers are unsigned; native interactive installer/playback validation, macOS notarization, and wider platform QA remain. |
| Help fidelity | Help content and navigation are functional, but exact native WinHelp typography and paragraph geometry are intentionally approximated by the cross-platform viewer. Unsafe legacy macro/external-file execution remains disabled. |
| Compatibility policy | Recreation save/replay formats may change before 1.0.0. Post-1.0 migration guarantees still need a release policy. Importing or exporting original 1996 save files is not planned. |

### Permanent scope boundaries

- Original copyrighted assets are never bundled; a supported legal copy is
  required for import.
- Original 1996 save import/export is not supported.
- WinSock, IPX, modem, serial, AppleTalk, and other legacy protocol
  interoperability will not be recreated. Online play uses the new documented
  transport instead.
- Legacy Help macros and external-file execution are not run.

## Quality-of-life additions

New Chrome keeps compatibility behavior as the default while adding optional or
presentation-only conveniences that make the original systems easier to read:

- Built-in cross-platform Help opens with F1 and uses the locally imported
  original manual topics, contents order, links, and definition popups. Verified
  executable formulas are added inside their relevant subjects; an incomplete
  compatible help pack receives a clearly named listed subject for any missing one.
- Hover tooltips explain the practical effects of city statistics, gang and
  site attributes, item modifiers, every game mode and duration, setup
  difficulty, and every Options entry.
- The city console shows projected turn cashflow beside current Cash, with
  finance panels breaking down upkeep, purchases, taxes, site income, Chaos,
  and the resulting adjustment.
- Command pickers name their valid gang, sector, site, and item targets. In the
  detailed-sector view, hovering an assigned gang highlights its queued Move,
  Influence, or Attack target directly on the board, building, or gang card.
- Research lists accumulated progress beside its required total, and report
  panels retain unread/page progress so information is not silently consumed.
- Nine named save slots show when and how each match was played; Escape pauses
  into save/load controls before offering a confirmed return to the main menu.
- Windowed and borderless-fullscreen modes can be toggled globally with F11 or
  Alt+Enter, and foreground panel motion can be disabled without changing game
  rules or deterministic state.

## Controls

| Action | Keyboard | Mouse |
|---|---|---|
| Select a sector | Arrow keys or WASD | Click a sector |
| Open or confirm | Enter | Double-click the selected sector or click a panel control |
| Cycle gangs | G | Click a gang card |
| Commands | C | Click the command control |
| Hire | H | Click Hire; drag an offer onto a controlled sector |
| Detailed sector | I | Double-click a sector |
| Finances | F | Click Finance |
| Ranking | R | Click Ranking |
| Research and equipment | T | Click Research or Equipment |
| Combat summary | B | Click Combat Summary |
| Search: Sites | X | Click Search |
| View/Send Comlink | M / N | Click the matching Comlink control |
| Scenario information | J | Click Game Info |
| Presentation and audio options | O | Click Options, then adjust the available gameplay-presentation, display, and audio choices |
| Windowed/fullscreen display | F11 or Alt+Enter | Use either shortcut from any screen; the choice is remembered between launches |
| Planning timer (setup) | L | Click None, 30 Seconds, 2 Minutes, or 5 Minutes |
| Help | F1 | Click Help on the title screen; point at the topic list or article and use the mouse wheel to scroll it |
| Online play | Tab between fields, Enter to host or join | Click Online on the title screen, then Host or Join |
| Save / load | F5 / F9 | Use Save Game or Load Game in the Escape menu and choose one of nine slots |
| Save / load replay | F6 / F10 | Local games only; records or verifies the recreation replay file |
| Finish planning | Space | Click the end-turn control |
| Pause/game menu | Escape | Resume, save, load, or request a confirmed return to the main menu |

## Crash reports

The game keeps up to five small local session logs and ten crash reports in
`%LOCALAPPDATA%\ChaosOverlordsNewChrome\Logs`. They contain technical lifecycle
and match-flow details, but no player names, commands, save contents, or asset
paths. Nothing is uploaded automatically.

## Acknowledgements

First and foremost, thank you to John K. Morris and the entire original
[*Chaos Overlords* team](https://www.mobygames.com/game/2455/chaos-overlords/credits/windows/)
at Stick Man Games and New World Computing. Their wonderfully strange,
uncompromising strategy game is the reason this recreation exists.

Huge thanks to [wfr](https://github.com/wfr) for publishing the
[*re-chaos* reverse-engineering notes](https://github.com/wfr/re-chaos). Their
careful early format research gave this project a tremendously useful head
start.

Special thanks as well to Russell Webb, with contributors Drew Fudenberg,
Tim Jordan, Adam K. Rixey, and George Ruof, for the remarkably thorough
[*Chaos Overlords* FAQ](https://gamefaqs.gamespot.com/pc/196900-chaos-overlords/faqs/1684).
It has been invaluable for clarifying game mechanics whose presentation in the
original game and manual can otherwise be delightfully cryptic.

This project copies no source code and redistributes no copyrighted resources
from the original game. Players are expected to buy and own a legal copy, such
as the [GOG release](https://www.gog.com/en/game/chaos_overlords), and import
its assets locally during installation.

## License

Copyright (C) 2026 kibertoad.

The original code in this repository is licensed under the
[GNU General Public License v3.0](LICENSE). The license does not cover or grant
rights to the original *Chaos Overlords* assets, which are not distributed by
this project.
