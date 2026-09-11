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
   The GOG release is fully compatible with the new runtime.
2. Download and run the latest **Chaos Overlords: New Chrome** Windows installer
   from [GitHub Releases](https://github.com/kibertoad/chaos-overlords-new-chrome/releases/latest).
3. Let Setup detect your GOG installation, or select its folder when prompted.
   Setup verifies and imports the required assets, then installs the new runtime.

The installer contains no original assets and requires an installed legal copy
when importing them.

## Current playable slice

| Area | Supported now | Not yet / current limitation |
|---|---|---|
| Installation and assets | Windows installer detects or prompts for the legal GOG installation, verifies it, repairs the original 16-bit graphics, and imports the required media. Linux and macOS packages can also import assets. | By design, original assets are never bundled and must be imported from a legal copy. All installers are currently unsigned. |
| Game setup | All ten scenarios, four durations, the global AI Mentality setting, atlas-aligned original portrait cards, one-to-six configured local human players, and the recovered optional 30-second/2-minute/5-minute planning timer. Add/Remove changes the human count; omitted color slots become computer players at Begin, so every match has six participants. | Original seed/setup fixture and golden-screen verification remain. |
| Local play | Complete deterministic hot-seat turn flow across Upkeep, Command, Execution, Hire, and Elimination, with the original handoff screen between human players. The recovered optional warning prevents accidentally finishing planning while active gangs lack commands. | Legacy protocol compatibility is a permanent non-goal. |
| Online play | Host or join a match from the title screen against a self-hostable or central coordination server (`multiplayer/`), which seals each simultaneous turn, relays the order set and verifies every client's state hash. Every client resolves the turn through the same deterministic core, so a disagreement is caught the turn it happens; see [`docs/MULTIPLAYER.md`](docs/MULTIPLAYER.md). | No lobby browser, no chat, no Comlink, and no joining a match already in progress. A seat whose player leaves stops being waited on and its gangs hold position rather than being taken over by the computer. Desync recovery waits on the host to send a snapshot. |
| City and sector UI | Native 640x460 presentation with integer-friendly scaling, persistent windowed/borderless-fullscreen display, grid-aligned ownership-composited city art, selectable 8x8 city, detailed 3x3 sector view with a six-gang card grid, gang/site information, finance, ranking, research, equipment, hire, sector-paged combat results, search, events, endgame, and a modern viewer for the locally imported original help. The upper-right city statistics have explanatory hover text, and Cash includes the current projected adjustment. The viewer preserves authored font emphasis and all internal jump/popup links. Keyboard and mouse navigation are supported, including right-click cancellation of transient interactions and nested panels. Options can show base or current gang statistics and enable or disable foreground-only panel motion. | Remaining original hit maps, configurable bindings, exact WinHelp typography/paragraph geometry, and golden-screen alignment are unfinished. |
| Gangs and commands | All 14 original command types are represented, validated, queued, cancelled, and resolved. The recovered recurring subset is Bribe, Chaos, Control, Heal, Hide, Influence, Research, and Snitch; incompatible one-off actions are omitted from the recurring picker. Hide follows the recovered active-action lifecycle: one-off Hide expires at the next Upkeep, recurring Hide remains active, and replacing or cancelling it reveals the gang immediately. Drag-to-move, drag-to-hire, Give, Sell, equipment replacement, research, Influence, Chaos, Control, Hide, Heal, Bribe, Snitch, and combat are playable. | Some resolver edge ordering, special-building boundaries, and exact message wording still need reference validation. |
| Hiring and economy | Original three-offer hire dock, automatic next-turn replacement portraits, comparison panel, snubbing, Force generation, cash, income, upkeep, debt restrictions, equipment purchasing, and persistent research progression with current/required progress display. | A few locality, rounding, and failure-edge behaviors remain under reverse engineering. |
| Combat and police | Simultaneous gang combat, retaliation, evasion, detection, casualties, equipment loss, Crackdowns, and executable-matched police attacks using the exact visible/Hide detection curves and Police Force 5 + Combat 20 − effective Defense pool. The original combat/result panels use fitted sector apertures, decoded animations and cadence, and equipped, unarmed, and detected-police sound cues. Detailed Combat controls automatic bounded animation playback without changing resolution; Simple Combat leaves the non-animated summaries available without blocking input. Selected summary results can be replayed in Detail mode and cancelled immediately. | Color-key details, bare-hand presentation, native targetability fixtures, and police notification edges remain to validate. |
| Objectives and endgame | All ten scenarios use the recovered score table and competition standings, with player-slot tie order and eliminated players trailing unranked. Timed and objective completion, the global sole-survivor rule, tied winners, five awards, statistics, elimination cleanup, victory/defeat routing, and playable Siege setup with six starting-sector pylon markers are implemented. | Special objective edges, exact Siege pylon art, hot-seat result sequencing, and final presentation details remain provisional. |
| Computer players | Deterministic objective-aware AI with recovered mentality, attitudes, reactions, live-equivalent offer refills, hiring, placement, difficulty calibration, handlers for every original strategy family wired into gameplay, and deterministic/replay stress coverage for six-player matches. Forty-turn objective campaigns must expand and hire, and Big Man completes naturally by turn 60 at the guarded seed. | Fixed original-reference boundaries, broader multi-seed tournaments, and reliable completion policy for five objective scenarios remain incomplete; unsupported decision edges retain an isolated provisional fallback. |
| Saves and replays | F5/F9 recreation-native save/load, end-turn autosaves, F6/F10 deterministic record/playback verification, bounded loading, read-back-before-promotion, and last-valid-generation backup recovery for both saves and replays. Current formats are save v19, replay v21, and canonical hash v22. | These formats may change incompatibly before 1.0.0. Original 1996 save import/export is not supported. |
| Audio, music, and video | Original audio, eight music tracks, and two videos are extracted; Detailed Combat synchronizes equipped, unarmed, retaliation, and detected-police cues to their corresponding clips, while full local-setup push buttons, setup selection/rejection, panel confirmation, pending Last Turn Events, and planning-timer warnings play their mapped original sounds. The recovered soundtrack uses Track 2 for title/setup, Tracks 3-8 for gameplay, and Track 9 for endgame, with repeat and focus pause/resume. Options exposes independent original 0-10 Music and Sound Effects scales, using the recovered defaults of 5 and 6 respectively and remembered between launches. | Exact menu restart boundaries and native-platform playback still need validation. Video playback and the remaining UI/impact sound triggers are not wired yet. |

## Quality-of-life additions

New Chrome keeps compatibility behavior as the default while adding optional or
presentation-only conveniences that make the original systems easier to read:

- Built-in cross-platform Help opens with F1 and uses the locally imported
  original manual topics, contents order, links, and definition popups. Verified
  executable formulas are added inside their relevant subjects; an incomplete
  compatible help pack receives a clearly named listed subject for any missing one.
- Hover tooltips explain the practical effects of city statistics, gang and
  site attributes, item modifiers, setup difficulty, and every Options entry.
- The city console shows projected turn cashflow beside current Cash, with
  finance panels breaking down upkeep, purchases, taxes, site income, Chaos,
  and the resulting adjustment.
- Command pickers name their valid gang, sector, site, and item targets. In the
  detailed-sector view, hovering an assigned gang highlights its queued Move,
  Influence, or Attack target directly on the board, building, or gang card.
- Research lists accumulated progress beside its required total, and report
  panels retain unread/page progress so information is not silently consumed.
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
| Search | X | Click Search |
| Presentation and audio options | O | Click Options, then adjust gang statistics, detailed combat, panel motion, idle warnings, Music, and Sound Effects |
| Windowed/fullscreen display | F11 or Alt+Enter | Use either shortcut from any screen; the borderless-fullscreen choice is remembered between launches |
| Planning timer (setup) | L | Click None, 30 Seconds, 2 Minutes, or 5 Minutes |
| Help | F1 | Click Help on the title screen; point at the topic list or article and use the mouse wheel to scroll it |
| Online play | Tab between fields, Enter to host or join | Click Online on the title screen, then Host or Join |
| Finish planning | Space | Click the end-turn control |
| Return to title | Escape | Use the on-screen back/cancel control where available |

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
