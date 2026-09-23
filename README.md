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

New Chrome is a broad, playable pre-1.0 recreation. The functional migration is
substantially complete: a full match can be played locally from setup through
results, and the deterministic core, original asset importer, modern online
transport, native saves, and replays are all operational. The remaining work is
primarily original-runtime corroboration, golden-screen and input comparison,
release hardening, and optional online experience rather than missing basic
match flow.
This is not yet a claim of pixel-perfect or rule-perfect parity with the shipped
1996 executable. The detailed evidence and next proof gate for each system are
tracked in the [parity matrix](docs/PARITY-MATRIX.md).
The technical documentation is cataloged and indexed in
[docs/README.md](docs/README.md).

### Faithfulness audit (2026-09-20)

The documentation maps the 29 currently identified in-scope game subsystems to
their rules, executable findings, implementation/parity rows, and deliberate
deviations. It contains 104 stable `BIN-*` findings and 24 `RULE-*` entries,
and its generated indexes and relative links are current. This is broad,
traceable coverage of the recovered structure; it is **not** evidence that the
entire original program structure or every hidden behavior has been recovered.

The audit does **not** validate New Chrome as fully compliant with the shipped
1996 logic. The matrix's only explicit `Parity verified` result is the
values-only gameplay-table row. Native launch-seed/RNG correlation, initial
city and transaction fixtures, full Original-AI decision traces, several
objective/police edges, and native visual/input/media comparisons remain open.
Consequently, the implementation should be regarded as a broad, deterministic,
evidence-led recreation with high-confidence static coverage in many areas,
not as a rule-perfect replica. Intentional deviations and quality-of-life
extensions are separately recorded in [DECISIONS.md](docs/DECISIONS.md) and
the [parity matrix](docs/PARITY-MATRIX.md).

### Implemented

| Area | Available now |
|---|---|
| Installation and assets | Windows, Linux, and macOS packaging can verify a supported legal GOG installation and import the required assets. The transactional extractor repairs RGB555 graphics, decodes indexed graphics and WinHelp content, imports audio/music/video, validates exact output inventories and hashes, and never copies the original executable. |
| New Game setup | All ten scenarios are mapped to their correct visual buttons and default to Kill 'Em All. Every mode and game-length button has a rules tooltip. The four duration choices are active only for the timed Greed, Power, Acceptance, and Dominance modes; objective modes run until their goal is reached, hide the duration selection light, and explain why duration clicks are disabled. Setup supports the global AI Mentality, the persisted default-off Advanced AI choice from Options, one-to-six named local humans, portrait/color rearrangement, six total participants after AI fill, and optional None/30-second/2-minute/5-minute planning clocks. |
| Local and hot-seat play | Deterministic turns run through Upkeep, Command, Execution, Hire, and Elimination. Multiple local humans receive the original private handoff screen. The optional idle-gang warning prevents accidental completion while an active gang has no order. |
| Commands and economy | All 14 original actions are validated, queued, cancelled, and resolved, with the recovered recurring subset enforced. Dragging a gang onto a neighboring sector queues Move, onto an eligible site queues Influence, and onto a visible enemy gang queues Attack. Hire, Reject, Equip, Give, multi-item Sell, Research, Bribe, Chaos, Control, Heal, Hide, Snitch, and Terminate are playable. The recovered player/roster scans, phase-opening snapshots, simultaneous Move-capacity normalization, attack-visibility gate, Influence takeover behavior, recurring-command cleanup, and end-turn scoring/cleanup are implemented. Cash, upkeep, sector tax, influenced-site income, debt restrictions, site effects, delayed Influence activation, and the recovered Factory/Sell rules are represented. |
| City, sector, and management UI | The 640x460 interface includes the 8x8 city, 3x3 detailed-sector neighborhood, up to six gang cards, three equipped-item cells in Gang Information, site/item details, City and Sector Finance, Ranking, Hire comparison, Comlink, Game Info, Research/Equipment, Give/Sell, Combat Summary/Detail, Options, Help, and endgame panels. Recovered setup-card hit geometry, main-console controls, shared-panel destinations, sector gang cards, status overlays, keyed and opaque sprite-copy modes, and bounded panel-slide distances now follow the native layouts. Mouse and keyboard input, right-click cancellation, nested-panel return, panel-motion control, and windowed/borderless-fullscreen presentation are wired. |
| Search and turn reports | Search: Sites exposes all 22 site types with ALL/NONE and individual filters. Controlled sites are always shown as white transparent markers; selected uncontrolled site types appear amber only while the overview is applied. Last Turn Events auto-opens when required, preserves unread progress until every page is viewed, remains reviewable afterward, and uses the recovered native site crop, palette stretch, ordered mask, foreground event art, and footer layout. Options can switch event-site backgrounds from Original to Smooth filtering. |
| Combat and police | Simultaneous combat, retaliation, Hide/evasion, casualties, equipment loss, Crackdowns, police detection and damage, and combat statistics are implemented with recovered player/roster ordering, phase snapshots, attack visibility, cooperative detection bands, and retired-gang equipment preservation. Summary results are grouped by sector; Detail uses the native combatant, equipment, Force-meter, and animation geometry to replay the chosen fight with decoded eight-frame animations and event-time equipped, unarmed, retaliation, and police sounds. Detailed/Simple presentation changes no authoritative outcome. |
| Objectives, ranking, and AI | All ten scenarios have timed/objective completion, recovered score tables, competition ranking, tied winners, sole-survivor handling, elimination cleanup, awards/statistics, and Siege objective markers. Deterministic computer players use the recovered difficulty bands, attitudes/reactions, hiring and placement rules, and handlers for every known strategy family. Original AI remains the parity default. Advanced AI composes documented policy deltas over that single planner: idle-gang recovery plus higher-difficulty outward movement for healthy gangs that would otherwise remain idle or repeat passive actions. F1 documents the exact order, thresholds, tie-breaks, and non-bonuses in game. |
| Saves and replays | Escape opens an in-game Resume/Save/Load/Options/Report-bug/Quit-to-main-menu menu; quitting requires confirmation that unsaved progress will be lost. F5/F9 open a nine-slot save/load browser. Saves suggest an editable name and display timestamp, scenario, single/hot-seat/online type, and human/AI counts. Atomic writes, backups, autosaves, and verified self-healing recovery of missing or corrupt primaries are implemented. F6/F10 save and verify deterministic local replays. Each slot also writes a compressed journal beside it, so a session’s whole history survives a save and a load rather than restarting at the load. Current formats are save v24, replay v28, and canonical hash v27. |
| Online play | The title screen can host named public or code-only matches through the central service at `chaos-overlords.dinorefurb.com` or a custom self-hosted coordination service. Public waiting and ongoing matches can be browsed and filtered by status, scenario, and AI difficulty. Hosts configure online games through the regular new-game setup, may start alone, and may allow late joiners to claim an AI seat that has never belonged to a human. Players choose the overlord face they sit down under when they create or join, beside the name they take with them, and the lobby roster shows every seat's face. The join-code field has a bounded clipboard Paste button, while lobby codes can be copied out. The escape menu of a match in progress shows that session's join code and password. The host uploads a verified rolling server autosave after every confirmed turn. The game retains up to eight incomplete memberships locally, suggests recovery after an unclean exit, and lets former players browse and reclaim their own seats, including seats temporarily transferred to AI. If the host is absent, the first returning player inherits that role. A departure or wholly missed timed turn opens a unanimous `WAIT`/`USE AI` vote; authenticated activity cancels a pending absence vote, approved takeover and return are recorded at a clean Command boundary, and reconnect reconstruction replays the decision gaplessly. Terminal multiplayer failures persist safe correlation details (operation, status/reason, request id, turn and event sequence) for local diagnostics without retaining credentials, names, settings, or orders. See [Multiplayer](docs/MULTIPLAYER.md). This does not reproduce the original network protocols. |
| Bug reports | The Escape menu can file a bug report to the project's server: a free-form description and, by default, the whole match as a compressed event-sourced journal that replays from its first turn. Player names become seat labels and Comlink text is redacted before it is compressed, by re-running the match so the result is a valid journal rather than an edited one. The box can be unticked, and what would be sent is spelled out beside it. |
| Help, media, and options | F1 opens a cross-platform viewer for the imported original Help contents, styles, internal jumps, and definition popups, augmented with verified executable formulas. The recovered title/setup, gameplay, and endgame music programs use the statically verified menu ownership and restart boundaries. Both Smacker movies stream through the managed decoder with deterministic cadence, PCM audio, explicit skip, focus pause, safe failure, first-run playback, and later access from the title screen's `INTRO` button. Independent 0-10 music/effect levels, mapped interface sounds, combat cues, planning warnings, display mode, gang-stat mode, combat detail, panel motion, idle warning, and event-image filter are implemented and persisted. Options can create a bounded, privacy-filtered diagnostics ZIP for explicit user sharing. |
| Engineering baseline | The authoritative simulation is headless and deterministic; saves, replays, online lockstep, and phase hashes share that state model. Automated coverage spans extraction, persistence/migrations, all command resolvers, scenarios, AI families, UI projections/layouts, networking contracts, installers, and multi-turn deterministic campaigns. Static-analysis findings, confidence, and unresolved behavior are documented rather than silently guessed. |

### Still missing or provisional

| Area | Remaining work |
|---|---|
| Exact gameplay parity | Capture native launch/setup/city fixtures that correlate the uptime-derived seed, startup `serialNum` state, and resulting RNG stream. Add runtime corroboration for the recovered transaction flow and the remaining Crackdown notification and special-objective edges that currently rely on static or manual evidence. |
| Original AI parity | Compare full native AI decisions and RNG consumption against fixed reference traces, expand multi-seed tournament coverage, and establish reliable evidence-led completion behavior for Kill 'Em All, Big 40, Eliminate, Siege, and Armageddon. Current AI is playable and deterministic, but complete outer-planner parity is not proven. |
| Visual and input parity | Complete the remaining management-workflow hit maps and golden-screen comparisons, especially Search and the city view; validate the remaining offsets, transparency/color keys, bare-hand style, endgame/hot-seat sequencing, and per-screen right-click behavior against native captures. Configurable key bindings and broader accessibility work are not implemented. |
| Online experience | Late joining is limited to eligible AI seats that have never belonged to a human. Spectating, lobby chat, and online Comlink integration are not implemented. A desync still depends on the host supplying a snapshot, and live-runtime recovery coverage will grow as more failure modes are identified. Security and deployment limitations are documented in [Multiplayer](docs/MULTIPLAYER.md). |
| Media and platform polish | Original movie trigger/skip capture, broader native A/V validation, the remaining interface/impact sound triggers, and sound overlap/interruption behavior remain. Windows releases can be Authenticode-signed through SSL.com eSigner and Linux `.deb` releases can carry a verified detached OpenPGP signature; macOS signing and notarization, native interactive installer validation, and wider platform QA remain. |
| Help fidelity | Help content and navigation are functional, but exact native WinHelp typography and paragraph geometry are intentionally approximated by the cross-platform viewer. Unsafe legacy macro/external-file execution remains disabled. |
| Compatibility and replay UX | Recreation save/replay formats may change before 1.0.0. Post-1.0 migration guarantees still need a release policy. Replays can be recorded and deterministically verified, but user-facing animated playback controls are not implemented. Importing or exporting original 1996 save files is not planned. |

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
  difficulty, and every Options entry. Resting the pointer on a gang command
  for two seconds explains what that order does before it is queued.
- The city console shows projected turn cashflow beside current Cash, with
  finance panels breaking down upkeep, purchases, taxes, site income, Chaos,
  and the resulting adjustment.
- Hovering a selected sector's Tolerance shows the active player's queued Chaos
  success range, calculated from its gangs' force, equipment, and local
  influenced sites. It also warns when known enemy gangs could add Chaos; the
  Tolerance value turns orange when the player's range can trigger a crackdown,
  and the tooltip explains the controlled/uncontrolled Chaos payout rule.
- Command pickers name their valid gang, sector, site, and item targets. In the
  detailed-sector view, hovering an assigned gang highlights its queued Move,
  Influence, or Attack target directly on the board, building, or gang card.
- Double-clicking an equipped item in Gang Information opens its Item
  Information panel; closing it returns to the same gang without changing the
  authoritative match state.
- The mouse-facing Give panel keeps its recovered original item and recipient
  cells, while Up/Down cycles eligible recipients as an additional keyboard
  navigation shortcut.
- Research lists accumulated progress beside its required total, and report
  panels retain unread/page progress so information is not silently consumed.
- Automatic Detailed Combat remains a bounded, skippable presentation over the
  already-resolved result. This intentionally fixes the original's known
  freeze while leaving combat rolls, state, and replay data unchanged.
- The default-on idle-gang warning catches an accidental end of planning while
  an active gang has no assigned order. It is an optional guard only: turning
  it off restores immediate completion, and neither choice changes simulation
  rules or saved match state.
- New local and online matches can optionally use 30-second, two-minute, or
  five-minute planning clocks instead of no clock. Advanced AI is likewise an
  explicit, default-off choice: it applies only when a future match is created,
  leaving active and loaded matches on their stored policy while Original AI
  remains the compatibility default. Game Information retains the native AI
  Mentality field and appends the stored `ORIGINAL` or `ADVANCED` policy label
  so that this deliberate gameplay choice is visible during a match.
- Online lobbies retain modern display names, while a started match uses the
  original game's deterministic ten-character, upper-case name record. Names
  that would become a native cheat code under that projection are refused and
  never reach match state.
- Keyboard navigation is available throughout the compatible mouse panels, and
  Escape or a right-click consistently cancels the current transient panel,
  drag, warning, or presentation without changing an unconfirmed command.
- Event-site artwork defaults to its recovered original crop and mask, with an
  optional Smooth view for readers who prefer an unmasked, linearly filtered
  background; this changes presentation only.
- Nine named save slots show when and how each match was played; Escape pauses
  into save/load controls before offering a confirmed return to the main menu.
- Escape also offers Report Bug, which sends a written description and — unless
  the box is unticked — an anonymized, replayable journal of the whole match, so
  a deterministic bug arrives as something that can be reproduced rather than
  described.
- The title screen names the build it is, and the same version travels with
  every bug report, crash log, and support ZIP, so a report can be matched to
  the installer it came from.
- Windowed and borderless-fullscreen modes can be toggled globally with F11 or
  Alt+Enter, and foreground panel motion can be disabled without changing game
  rules or deterministic state.
- F12 captures the finished native window backbuffer as a timestamped PNG in
  the game-local `screenshots` folder; captures remain local and are ignored by
  Git.
- Online Lobby Appearance in Options switches between the modern lobby and a
  classic presentation based on the original host-lobby artwork. Both use the
  same public-listing, private join-key, late-join, and six-seat modern session
  flow; no legacy transport is enabled.
- Options can explicitly export a bounded support ZIP to the local application-
  data `Diagnostics` directory. It contains recent client/session events and
  privacy-filtered crash summaries for startup, display, audio, and crash
  troubleshooting. The ZIP stays local until the user shares it, contains no
  replayable match state, and omits names, commands, saves, Comlink messages,
  exception messages, and file paths. This complements Report Bug: that feature
  sends a written description and, optionally, an anonymized replay of the match.

## Headless AI tournaments

`Rechaos.Tools ai-tournament` runs computer-only matches directly against the
authoritative model. It does not construct the game window or execute graphics,
audio, input, animation, or real-time pacing. Cases use deterministic consecutive
seeds and can run concurrently on a bounded number of workers. The command writes
progress and optional per-turn traces to standard error, then emits one stable
JSON report to standard output:

```powershell
dotnet run --project src/Rechaos.Tools -c Release --no-build -- ai-tournament `
  --matches 60 --turns 40 --workers 2 --policy advanced `
  --scenarios objectives --replay-every 10
```

The default five-second heartbeat shows completed, running, and failed counts.
Use `--trace` for each match's turn, phase-boundary, event-count, and elapsed-time
checkpoints. `--replay-every 0` disables the comparatively expensive replay pass;
a positive value replay-verifies every Nth case while the other cases remain
bare-model simulations. Use the same seed, scenario set, turn horizon, and worker
count for statistically paired Original/Advanced runs.

Parallelism is across independent matches. Seats inside one match remain ordered
because planning preparation, hire offers, and command resolution consume shared
deterministic state and RNG; online transport may collect order documents
asynchronously, but every client still applies them in the same sealed order.

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
| Save a screenshot | F12 | Writes the finished native window backbuffer as a PNG to the game-local `screenshots` folder |
| Planning timer (setup) | L | Click None, 30 Seconds, 2 Minutes, or 5 Minutes |
| Help | F1 | Click Help on the title screen; point at the topic list or article and use the mouse wheel to scroll it |
| Online play | Tab between enabled fields; Left/Right turn your overlord face; Enter creates or connects; F5 refreshes the browser | The form asks what you want to do, who you are, which session, and last which server: pick Host A New Game or Join With A Code, the arrows beside the face pick it, Paste fills the join code, Browse Games and Unfinished Sessions are the other ways in, and Copy copies the join code from the lobby |
| Save / load | F5 / F9 | Use Save Game or Load Game in the Escape menu and choose one of nine slots |
| Save / load replay | F6 / F10 | Local games only; records or verifies the recreation replay file |
| Finish planning | Space | Click the end-turn control |
| Pause/game menu | Escape | Resume, save, load, adjust options, report a bug, or request a confirmed return to the main menu |
| Report a bug | Escape, then Report Bug | Tab moves between the box, the checkbox and the buttons; Enter is a new paragraph in the box |

## Crash reports

The game keeps up to five small local session logs and ten crash reports in
`%LOCALAPPDATA%\ChaosOverlordsNewChrome\Logs`. They contain technical lifecycle
and match-flow details, but no player names, commands, save contents, or asset
paths. Nothing is uploaded automatically.

## Documentation

[docs/README.md](docs/README.md) catalogs every technical document by purpose
and indexes them by game subsystem. The most-used entry points:

| If you want to… | Read |
|---|---|
| Build, run, and test from source | [Development guide](docs/DEVELOPMENT.md), [Validation procedure](docs/VALIDATION.md) |
| Resume development at the current checkpoint | [Handover](docs/HANDOVER.md), [Implementation plan](docs/IMPLEMENTATION-PLAN.md) |
| Know how faithful each system is | [Parity matrix](docs/PARITY-MATRIX.md), [Project decisions](docs/DECISIONS.md) |
| Understand the code layout | [Architecture](docs/ARCHITECTURE.md) |
| Look up a game rule or an executable finding | [Game rules](docs/GAME-RULES.md), [Original executable internals](docs/ORIGINAL-INTERNALS.md), [AI specification](docs/AI-SPEC.md) |
| Work on the original file formats or assets | [Original file formats](docs/ORIGINAL-FILE-FORMATS.md), [Asset catalog](docs/ASSET-CATALOG.md), [UI atlas](docs/UI-ATLAS.md), [Audio and video](docs/AUDIO-VIDEO.md) |
| Host or extend online play | [Multiplayer](docs/MULTIPLAYER.md), [Server operator manual](multiplayer/README.md) |
| Cut a release | [Building and releasing installers](docs/RELEASING.md) |

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
