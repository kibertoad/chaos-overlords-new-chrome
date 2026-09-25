# Documentation index

Status: maintained index

The documentation of *Chaos Overlords: New Chrome* is in three places. The
[spec](../spec/README.md) describes the original game and nothing else, one entry
per finding, rule, format, screen and bug, as version 1 of the
[documentation standard](https://dinorefurb.com/documentation-standard/) defines
it. The ledgers at the repository root describe the rebuild against the spec:
[PARITY.md](../PARITY.md) says how much of each spec entry the rebuild does and
which tests prove it, and [DEVIATIONS.md](../DEVIATIONS.md) lists every
deliberate departure. This directory holds everything else: how the rebuild is
designed, built, validated and released. Player-facing material is in the
repository [README](../README.md); working rules for agents and contributors are
in [AGENTS.md](../AGENTS.md).

<!-- doc-index:begin toc depth=2 -->
- [Start here](#start-here)
- [Document catalog](#document-catalog)
- [Topic index](#topic-index)
- [Identifiers and conventions](#identifiers-and-conventions)
- [Canonical identities](#canonical-identities)
- [Maintaining this directory](#maintaining-this-directory)
<!-- doc-index:end -->

## Start here

| If you want to… | Open |
|---|---|
| Build, run, and test from source | [DEVELOPMENT.md](DEVELOPMENT.md), then [VALIDATION.md](VALIDATION.md) for the fast gate, the long-running tier and the fixture classes |
| Resume development at the current checkpoint | [HANDOVER.md](HANDOVER.md), then [IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md) for the roadmap |
| Look up what the original does | The [spec](../spec/README.md), through its indexes [by area](../spec/index/by-area.md), [by kind](../spec/index/by-kind.md) and [by status](../spec/index/by-status.md), or the [topic index](#topic-index) below |
| Know how faithful a system is and what proves it | [PARITY.md](../PARITY.md) |
| Check whether a difference from the original is deliberate | [DEVIATIONS.md](../DEVIATIONS.md), and [DECISIONS.md](DECISIONS.md) for the reasoning |
| Find the spec entry an old `BIN-*` or `RULE-*` citation meant | [SPEC-ID-MAP.md](SPEC-ID-MAP.md) |
| Pick up research work | [static_validation_plan.md](../static_validation_plan.md) for work in Ghidra and on the data files, [manual_validation_plan.md](../manual_validation_plan.md) for runs of the original that need a person |
| Write a new spec entry | [SPEC-ENTRY-TEMPLATES.md](SPEC-ENTRY-TEMPLATES.md), and the rules in [AGENTS.md](../AGENTS.md#the-spec) |
| Understand how the rebuild's computer players are built | [AI-SPEC.md](AI-SPEC.md); the original's planner is in the spec's `AI` area |
| Find your way around the code | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Work on the asset pack | [ASSET-PACK.md](ASSET-PACK.md), [ASSET-CATALOG.md](ASSET-CATALOG.md), [AUDIO-VIDEO.md](AUDIO-VIDEO.md) |
| Set up binary analysis or capture the original running | [GHIDRA.md](GHIDRA.md), [REFERENCE-CAPTURE.md](REFERENCE-CAPTURE.md) |
| Change saves, replays, or the canonical hash | [NATIVE-SAVE-FORMAT.md](NATIVE-SAVE-FORMAT.md) |
| Host, operate, or extend online play | [MULTIPLAYER.md](MULTIPLAYER.md) for the design, [MULTIPLAYER-REVIEW.md](MULTIPLAYER-REVIEW.md) for the review items still open, [multiplayer/README.md](../multiplayer/README.md) for operating a server, [src/Rechaos.Multiplayer/README.md](../src/Rechaos.Multiplayer/README.md) for the game client; version rules are in [AGENTS.md](../AGENTS.md#multiplayer-protocol-version) |
| Move turn resolution onto the server or port the core to TypeScript | [SERVER-AUTHORITATIVE-REPLAY.md](SERVER-AUTHORITATIVE-REPLAY.md) |
| Cut or sign a release | [RELEASING.md](RELEASING.md) |

## Document catalog

Documents in this directory open with a `Status:` line saying how settled they
are. Git history provides change dates.

### Orientation, process, and status

| Document | What it holds | Kind |
|---|---|---|
| [DEVELOPMENT.md](DEVELOPMENT.md) | Running from source, extractor commands, the fast validation gate, project list | Guide |
| [HANDOVER.md](HANDOVER.md) | Repository state, current format versions, the latest playable work by area, open conformance work, and where the next agent starts | Living checkpoint |
| [IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md) | Definition of complete, baseline, engineering principles, documentation and evidence rules, workstreams A–N, milestones M0–M8, test matrix, completion conventions, source hierarchy | Roadmap |
| [DECISIONS.md](DECISIONS.md) | Dated product, compatibility, and scope decisions, newest first, with an index | Decision log |
| [VALIDATION.md](VALIDATION.md) | Validation layers, `Invoke-Validation.ps1` modes, canonical identities, experiments on the original, static research, spec checks, tests against the original, fixture classes, failure triage | Procedure |
| [RELEASING.md](RELEASING.md) | `version.txt`, local package builds, the release workflow, signing, continuous integration | Procedure |
| [SPEC-ENTRY-TEMPLATES.md](SPEC-ENTRY-TEMPLATES.md) | A blank entry of each spec kind | Template |
| [SPEC-ID-MAP.md](SPEC-ID-MAP.md) | The spec entry that holds each finding and rule the documentation cited before the standard | Frozen map |

### Rebuild design and formats

| Document | What it holds | Kind |
|---|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Dependency direction, the projects and their responsibilities, command and event flow, the turn model as implemented, state ownership, determinism and proprietary-content boundaries, online play, error and security model, known debt | Design |
| [AI-SPEC.md](AI-SPEC.md) | The rebuild's Original and Advanced computer-player policies, planner inputs and invariants, and the parity work still required | Design |
| [NATIVE-SAVE-FORMAT.md](NATIVE-SAVE-FORMAT.md) | The rebuild's save container and limits, the save document, the compatibility policy, and the replay format | Format specification |
| [ASSET-PACK.md](ASSET-PACK.md) | The asset-pack fingerprint the extractor checks and the gameplay data bundled from the original tables | Reference |
| [ASSET-CATALOG.md](ASSET-CATALOG.md) | Every extracted output with its source, conversion, role and hash; regenerated by `Rechaos.Extractor --catalog`, never edited by hand | Generated inventory |
| [AUDIO-VIDEO.md](AUDIO-VIDEO.md) | How the rebuild plays sound effects, music and the Smacker movies | Media map |
| [MULTIPLAYER.md](MULTIPLAYER.md) | The coordination server: transport, protocol and session versions, lobby, turn barrier, timers, bug-report intake, retention, security, the shared contract, client integration | Design |
| [MULTIPLAYER-REVIEW.md](MULTIPLAYER-REVIEW.md) | What is still open from the review of online play | Review |
| [SERVER-AUTHORITATIVE-REPLAY.md](SERVER-AUTHORITATIVE-REPLAY.md) | Design for server-side turn resolution and the engine conformance corpus | Design (not implemented) |

### Research tooling

| Document | What it holds | Kind |
|---|---|---|
| [GHIDRA.md](GHIDRA.md) | The pinned Ghidra and JDK installation, the reference executable and manual, the headless script workflow under `tools/ghidra/`, and evidence discipline | Tooling guide |
| [REFERENCE-CAPTURE.md](REFERENCE-CAPTURE.md) | Operating the original executable and the window-capture helper, and the rules for captures | Procedure |

### Machine-readable

| Path | What it holds |
|---|---|
| [schemas/reference-fixture.schema.json](schemas/reference-fixture.schema.json) | JSON Schema for sanitized reference fixtures described in [VALIDATION.md](VALIDATION.md#fixture-classes) |
| [schemas/state-labels.example.json](schemas/state-labels.example.json) | Example state-label document for fixtures |

### Outside this directory

| Path | What it holds |
|---|---|
| [spec/](../spec/README.md) | The original game: builds, sources, findings, experiments, formats with their Kaitai definitions, rules, bugs, screens, the glossary, and the generated indexes |
| [PARITY.md](../PARITY.md) | One row per rule, format and screen entry: how much of it the rebuild does, the tests that compare it with the original, and the deviations from it |
| [DEVIATIONS.md](../DEVIATIONS.md) | Every deliberate departure from the spec, as `DEV-*` entries with their settings |
| [static_validation_plan.md](../static_validation_plan.md) | Open questions that a reading of the executable or the data files can settle |
| [manual_validation_plan.md](../manual_validation_plan.md) | Open questions that need a person to run the original |
| [README.md](../README.md) | Player-facing overview, quick start, controls, acknowledgements, licence |
| [AGENTS.md](../AGENTS.md) | Working rules: push destination, validation scope, documentation, fidelity, version rules, orphan-process audit |
| [multiplayer/README.md](../multiplayer/README.md) | Operator and contributor manual for the coordination server |
| [src/Rechaos.Multiplayer/README.md](../src/Rechaos.Multiplayer/README.md) | The game's C# client for the coordination server |

## Topic index

Where each part of the game is documented. The spec column opens the area's
list of entries; the rows of the parity matrix are grouped by the same areas.

| Topic | Spec areas | Rebuild documents |
|---|---|---|
| Executable, platform and file locations | [EXE](../spec/index/by-area.md#exe), [PLATFORM](../spec/index/by-area.md#platform), [ASSET](../spec/index/by-area.md#asset) | [GHIDRA.md](GHIDRA.md) |
| Gameplay tables and other data files | [DATA](../spec/index/by-area.md#data) | [ASSET-PACK.md](ASSET-PACK.md), [ASSET-CATALOG.md](ASSET-CATALOG.md) |
| Images, sound, music and video | [GFX](../spec/index/by-area.md#gfx), [AUDIO](../spec/index/by-area.md#audio), [VIDEO](../spec/index/by-area.md#video) | [AUDIO-VIDEO.md](AUDIO-VIDEO.md) |
| Help | [HELP](../spec/index/by-area.md#help) | |
| Saves | [SAVE](../spec/index/by-area.md#save) | [NATIVE-SAVE-FORMAT.md](NATIVE-SAVE-FORMAT.md) |
| Shared state, randomness and the order of a turn | [STATE](../spec/index/by-area.md#state), [RNG](../spec/index/by-area.md#rng), [TURN](../spec/index/by-area.md#turn) | [ARCHITECTURE.md](ARCHITECTURE.md) |
| New-game setup and the city | [SETUP](../spec/index/by-area.md#setup), [CITY](../spec/index/by-area.md#city) | |
| Instant commands | [HIRE](../spec/index/by-area.md#hire), [HIDE](../spec/index/by-area.md#hide), [INFLUENCE](../spec/index/by-area.md#influence), [HEAL](../spec/index/by-area.md#heal), [RESEARCH](../spec/index/by-area.md#research), [BRIBE](../spec/index/by-area.md#bribe), [SNITCH](../spec/index/by-area.md#snitch) | |
| Sectors, sites and tolerance | [TOLERANCE](../spec/index/by-area.md#tolerance), [SITE](../spec/index/by-area.md#site), [MOVE](../spec/index/by-area.md#move), [CONTROL](../spec/index/by-area.md#control) | |
| Gangs, equipment and money | [GANG](../spec/index/by-area.md#gang), [EQUIP](../spec/index/by-area.md#equip), [GIVE](../spec/index/by-area.md#give), [SELL](../spec/index/by-area.md#sell), [TERMINATE](../spec/index/by-area.md#terminate), [UPKEEP](../spec/index/by-area.md#upkeep), [FINANCE](../spec/index/by-area.md#finance) | |
| Combat, detection, Chaos and police | [ATTACK](../spec/index/by-area.md#attack), [COMBAT](../spec/index/by-area.md#combat), [DETECT](../spec/index/by-area.md#detect), [CHAOS](../spec/index/by-area.md#chaos), [POLICE](../spec/index/by-area.md#police) | |
| Computer players | [AI](../spec/index/by-area.md#ai) | [AI-SPEC.md](AI-SPEC.md) |
| Reports, messages and search | [EVENT](../spec/index/by-area.md#event), [COMLINK](../spec/index/by-area.md#comlink), [SEARCH](../spec/index/by-area.md#search) | |
| Objectives, ranking and awards | [OBJECTIVE](../spec/index/by-area.md#objective), [AWARDS](../spec/index/by-area.md#awards), [TIMER](../spec/index/by-area.md#timer) | |
| Screens, options and input | [UI](../spec/index/by-area.md#ui), [OPTIONS](../spec/index/by-area.md#options) | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Network play | [NET](../spec/index/by-area.md#net) | [MULTIPLAYER.md](MULTIPLAYER.md); the original's network play is not reproduced (DEV-NET-001) |
| Validation and evidence | | [VALIDATION.md](VALIDATION.md), [GHIDRA.md](GHIDRA.md#evidence-discipline), [REFERENCE-CAPTURE.md](REFERENCE-CAPTURE.md#evidence-rules) |
| Packaging and releases | | [RELEASING.md](RELEASING.md) |

## Identifiers and conventions

| Convention | Meaning | Defined in |
|---|---|---|
| `KIND-AREA-NNN` | A spec entry: `FND` finding, `EXP` experiment, `FMT` format, `RULE` rule, `BUG` bug, `SCR` screen. Builds and sources use an alias (`BLD-GOG-EN-1.1`, `SRC-MANUAL-GOG`). IDs are never reused or renumbered. | [AGENTS.md](../AGENTS.md#the-spec) and the standard |
| `DEV-AREA-NNN` | A deliberate departure of the rebuild from the spec. | [DEVIATIONS.md](../DEVIATIONS.md) |
| `unknown` … `superseded` | The status of a spec claim; `recorded`, `reproduced` and `superseded` for findings and experiments. There is no other confidence scale. | [AGENTS.md](../AGENTS.md#the-spec) |
| `implemented`, `validated` | Parity statuses beyond a spec status. | [PARITY.md](../PARITY.md) |
| `PLACEHOLDER: <spec ID>` | A code comment marking a guessed value; its parity row cannot be `complete`. | [AGENTS.md](../AGENTS.md#the-rebuilds-ledgers) |
| `YYYY-MM-DD — <title>` | A dated decision. New entries go at the top of the decision log. | [DECISIONS.md](DECISIONS.md) |
| `Status:` | Header line describing how settled a document in this directory is. | This directory |
| `<!-- doc-index:begin … -->` | A generated table of contents or index. Rebuild with `node tools/update-doc-indexes.mjs`; `--check` fails when a block is stale, and either mode fails when a relative link or `#anchor` no longer resolves. | [tools/update-doc-indexes.mjs](../tools/update-doc-indexes.mjs) |

## Canonical identities

The spec identifies files by xxh3-128: the executable, every data file and the
manual are listed with their hashes in
[BLD-GOG-EN-1.1](../spec/builds/BLD-GOG-EN-1.1.md) and the source entries in
[spec/sources/](../spec/sources/). The rebuild's own checks use these SHA-256
values:

| Identity | SHA-256 | Authority |
|---|---|---|
| Reference executable `Chaos Overlords.exe` (version 1.1, 664,576 bytes) | `a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89` | [GHIDRA.md](GHIDRA.md#reference-executable) |
| Full `DATA` + `HELP` + `MUSIC` source pack | `ad958a934a691318f31a27a87252f420dd89a0ad03759457d8feaf49914d29e3` | [ASSET-PACK.md](ASSET-PACK.md), [VALIDATION.md](VALIDATION.md#current-canonical-identities) |
| Bundled gameplay JSON `src/Rechaos.Core/GameData/original-data.json` | `e65f80e4d9a99ceeffbbc7fb335ef7f57b368ef87c1c56af23bcd759cd4e8b3a` | [ASSET-PACK.md](ASSET-PACK.md) |

Current format versions (native save, replay, canonical hash, asset manifest,
extracted help, client preferences) change more often than hashes; read them
from [HANDOVER.md](HANDOVER.md#repository-state) and
[NATIVE-SAVE-FORMAT.md](NATIVE-SAVE-FORMAT.md).

## Maintaining this directory

- New evidence about the original goes into the spec as a new entry, never into
  this directory. A departure of the rebuild goes into DEVIATIONS.md, with its
  reasoning as a dated entry in DECISIONS.md when it is a product decision.
- Regenerate, do not hand-edit, generated blocks: run
  `node tools/update-doc-indexes.mjs` after changing headings here, and
  `node tools/check-spec.mjs` after changing the spec. ASSET-CATALOG.md is
  rewritten by `dotnet run --project src/Rechaos.Extractor -- --catalog`.
- A new document gets a `Status:` line, a row in the catalog above, and a row
  in the topic index when it covers part of the game.
