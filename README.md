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
   from [GitHub Releases](https://github.com/kibertoad/rechaos-overlords/releases/latest).
3. Let Setup detect your GOG installation, or select its folder when prompted.
   Setup verifies and imports the required assets, then installs the new runtime.

The installer contains no original assets and requires an installed legal copy
when importing them.

## Current playable slice

| Area | Supported now | Not yet / current limitation |
|---|---|---|
| Installation and assets | Windows installer detects or prompts for the legal GOG installation, verifies it, repairs the original 16-bit graphics, and imports the required media. Linux and macOS packages can also import assets. | By design, original assets are never bundled and must be imported from a legal copy. All installers are currently unsigned. |
| Game setup | All ten scenarios, four durations, the global AI Mentality setting, original portraits, and one-to-six explicitly configured human/computer players. Omitted slots become computer players, so every match has six participants. | Some setup-screen alignment and original hit regions still need parity work. |
| Local play | Complete deterministic hot-seat turn flow across Upkeep, Command, Execution, Hire, and Elimination, with the original handoff screen between human players. | Network multiplayer is not supported. Legacy protocol compatibility is a permanent non-goal; modern networking is post-parity work. |
| City and sector UI | Native 640x460 presentation with integer-friendly scaling, ownership-composited city art, selectable 8x8 city, detailed 3x3 sector view, gang/site information, finance, ranking, research, equipment, hire, combat summary, search, events, and endgame screens. Keyboard and mouse navigation are supported. | Remaining original hit maps, right-click/cancel behavior, configurable bindings, and golden-screen alignment are unfinished. |
| Gangs and commands | All 14 original command types are represented, validated, queued, repeated/cancelled, and resolved. Drag-to-move, drag-to-hire, Give, Sell, equipment replacement, research, Influence, Chaos, Control, Hide, Heal, Bribe, Snitch, and combat are playable. | Some resolver edge ordering, special-building boundaries, and exact message wording still need reference validation. |
| Hiring and economy | Original three-offer hire dock, comparison panel, snubbing, Force generation, cash, income, upkeep, debt restrictions, equipment purchasing, and research progression. | A few locality, rounding, and failure-edge behaviors remain under reverse engineering. |
| Combat and police | Simultaneous gang combat, retaliation, evasion, detection, casualties, equipment loss, Crackdowns, police attacks, original combat panels, decoded animations, and equipped-weapon sound cues. | Animation cadence/color-key details, bare-hand presentation, and several reveal/police ordering edges remain to validate. |
| Objectives and endgame | Live scenario scoring and completion, timed standings, tied winners, five awards, statistics, elimination cleanup, and an endgame summary. | Some objective timing/tie edges and final presentation details remain provisional. |
| Computer players | Deterministic objective-aware AI with recovered mentality, attitudes, reactions, hiring, placement, difficulty calibration, and handlers for every original strategy family wired into gameplay. | Fixed original-reference boundaries and larger-player stress coverage are still incomplete; unsupported decision edges retain an isolated provisional fallback. |
| Saves and replays | F5/F9 recreation-native save/load, end-turn autosaves, F6/F10 deterministic record/playback verification, bounded loading, and backup recovery. | These formats may change incompatibly before 1.0.0. Original 1996 save import/export is not supported. |
| Audio, music, and video | Original audio, eight music tracks, and two videos are extracted; equipped-weapon attacks play their mapped original sound cues. The recovered soundtrack uses Track 2 for title/setup, Tracks 3-8 for gameplay, and Track 9 for endgame, with repeat and focus pause/resume. An Options panel exposes the recovered 0-10 music scale, starts at the original level 5, and remembers the selected level between launches. | Exact menu restart boundaries and native-platform playback still need validation. Video playback and most UI/police/impact sound triggers are not wired yet. |

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
| Music options | O | Click Options on the title screen, then click a volume level |
| Finish planning | Space | Click the end-turn control |
| Return to title | Escape | Use the on-screen back/cancel control where available |

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
