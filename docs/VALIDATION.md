# Recreation validation procedure

Status: maintained canonical procedure

<!-- doc-index:begin toc depth=2 -->
- [Validation layers](#validation-layers)
- [Local automated checks](#local-automated-checks)
- [Current canonical identities](#current-canonical-identities)
- [Experiments on the original](#experiments-on-the-original)
- [Static binary research](#static-binary-research)
- [Spec checks](#spec-checks)
- [Tests against the original](#tests-against-the-original)
- [Fixture classes](#fixture-classes)
- [Native audio backend](#native-audio-backend)
- [Native pattern fill reference](#native-pattern-fill-reference)
- [Original pattern resources](#original-pattern-resources)
- [Failure triage](#failure-triage)
- [First-planning map comparison without the keyboard footer](#first-planning-map-comparison-without-the-keyboard-footer)
- [All active-player marker frames in the first planning view](#all-active-player-marker-frames-in-the-first-planning-view)
- [Both selected-sector outline states](#both-selected-sector-outline-states)
- [Completed-state final calendar and report capture](#completed-state-final-calendar-and-report-capture)
- [Final-entry hire dock comparison](#final-entry-hire-dock-comparison)
- [Completed-state local waiting-light comparison](#completed-state-local-waiting-light-comparison)
<!-- doc-index:end -->

## Validation layers

Accuracy is established separately at four layers, and a pass at one layer does
not imply a pass at the next:

1. **Source identity** - original input files match the hashes in their build
   entry, `spec/builds/BLD-GOG-EN-1.1.md`.
2. **Decode fidelity** - the decoders read every byte of every file a format
   entry lists into the value its Kaitai definition gives.
3. **Behavioral parity** - the same starting state and inputs produce the state
   changes and events an experiment fixture recorded in the original.
4. **Presentation parity** - the same state produces the screen a capture of the
   original shows, pixel for pixel, and starts the same sounds on the same tick.

`PARITY.md` records which rows have tests at these levels.

## Local automated checks

```powershell
./tools/Invoke-Validation.ps1
```

This is the canonical local validation entry point. It serializes runs for the
checkout and caps MSBuild at two workers by default. Before building, it stops
only a `Rechaos.Game` process whose executable lives inside this checkout; an
installed copy and unrelated `dotnet` processes are left alone. MSBuild and
Roslyn server reuse are retained because both materially speed repeated builds.

On Windows, when the first `dotnet` on PATH is a command shim such as
`dotnet.cmd`, cmd.exe would read the `&` and `|` of a compound test filter as
shell operators. The gate then runs a native host instead: the `dotnet.exe` in
`DOTNET_ROOT`, else the first `dotnet.exe` on PATH, taking only one with an
`sdk` directory beside it so a runtime-only install is skipped. With no such
host it warns and keeps the shim. Otherwise, and on Unix, it runs `dotnet` from
PATH. A focused run that matches no test fails:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Invoke-Validation.ps1 -TestFilter 'FullyQualifiedName~AudioRoutingTests|FullyQualifiedName~SoundtrackCatalogTests'
```

Every run is a cold build. Since `de425b9` the script restores, builds and tests
into a fresh GUID-named directory under the temporary root and deletes it
afterwards, so no `obj` or `bin` from a previous run — or from an editor — takes
part. A run killed before it can clean up leaves that tree behind; the next run
for the same checkout removes any it finds while it holds the lock.

It also runs `node tools/update-doc-indexes.mjs --check`, which fails on a stale
generated index block or a relative link between documents that no longer
resolves. The check runs before the build but its failure is raised only after
the tests, so it never hides a build or test result. Without Node.js on the path
it is skipped with a warning locally; when `CI` is set, a missing `node` fails the
run instead.

The default gate excludes only the 53-case `LongRunning` AI campaign category.
At the current checkpoint it builds with zero warnings and runs about 2,550
focused tests in well under a minute. These retain deterministic planner, Advanced-policy,
headless-runner, replay, persistence, and bounded single-case behavior coverage;
the exclusion is the repeated 20/40/60-turn, multi-seed statistical campaign
matrix, not the AI unit and integration tests.

Run every test, including the campaigns, explicitly:

```powershell
./tools/Invoke-Validation.ps1 -IncludeLongRunningTests
```

The complete gate contains 1,575 tests; its 53-test long-running tier most recently passed
independently in 8 minutes 35 seconds. Both tiers
carry exact minimum discovery counts so accidentally excluding or failing to
discover tests fails the gate. This is an implementation regression baseline,
not a measure of parity completeness.

For an investigation, run only the long category with live output. Its cases
record scenario and seed at startup, then report turn, phase-boundary count,
event count, and elapsed time every ten turns so a slow run can be distinguished
from a stalled one:

```powershell
./tools/Invoke-Validation.ps1 `
  -LongRunningTestsOnly `
  -TraceTestOutput
```

`-TestFilter` can select any narrower test slice; `-TraceTestOutput` exposes
captured test output and completed-case names. A filtered run still leaves the
long category out (the filter is combined with `Category!=LongRunning`) unless
`-IncludeLongRunningTests` is also passed, and fails when the filter selects no
test at all. The default gate requires 3,060 cases to be discovered, the
long-only mode all 53, and the full suite their sum, 3,113; the counts come from
`dotnet test --project tests/Rechaos.Tests --list-tests` with the same filters
and are raised together when tests are added. `-LongRunningTestsOnly` cannot be
combined with either of the other two, so the selected scope remains
unambiguous. Every invocation retains the 30-minute global test safety timeout.

The manually dispatched CI workflow runs the fast tier on Windows x64, Linux
x64, macOS arm64, and macOS x64. Its `fast-and-long-running` option adds the
observable long category once on Linux. A separate workflow runs the observable
long category on Linux every day at 03:17 UTC, but skips scheduled execution when
the default branch has no commit from the preceding 24 hours. Manual dispatches
always run it. The release workflow runs the fast tier only, leaving the repeated
statistical campaign matrix off its critical path.

For larger statistical samples, the presentation-free runner avoids xUnit and
lets replay verification be sampled rather than paid for on every match. It
runs computer-only matches against the authoritative model without building the
game window or running graphics, audio, input, animation, or real-time pacing.
Cases use consecutive seeds and run on a bounded number of workers. Progress and
the optional per-turn trace go to standard error, and one JSON report goes to
standard output:

```powershell
dotnet run --project src/Rechaos.Tools -c Release --no-build -- ai-tournament `
  --matches 60 --turns 40 --workers 2 --policy original `
  --scenarios objectives --replay-every 10 --trace
```

The heartbeat, every five seconds by default, shows completed, running, and
failed counts without `--trace`; the trace adds each live case's scenario, seed,
turn, boundary count, event count, and elapsed time. `--replay-every N`
replay-verifies every Nth case and leaves the others as bare-model simulations;
`--replay-every 0` turns replay verification off. The final JSON includes
deterministic hashes and territory, defended-territory, gang, combat, replay,
and timing metrics. For a paired comparison, run the same seeds, scenario set,
turn horizon, and worker count with `--policy advanced`.

The workers run separate matches in parallel. Seats inside one match stay
ordered, because planning preparation, hire offers, and command resolution
consume shared deterministic state and RNG. Online play may collect order
documents asynchronously, but every client applies them in the same sealed
order.

### Simulated human seats

Several AI paths react only to human players: the hunters pick their targets
among human gangs, many families treat a human owner differently, and a match
ends when its only human is eliminated. A match of computer players never
reaches them. A simulation that needs them seats a simulated human: the seat
registers as `PlayerController.Human`, so every rule and AI query sees a
person, and the computer planner plays it.

```csharp
var result = HeadlessMatchRunner.Run(definitions, new HeadlessMatchOptions(
    ScenarioId.Dominance, GameDuration.FourYears, seed,
    SimulatedHumans: [new PlayerId(0)]));
```

The named seats are set up as human and the others as computer players;
seats the options do not reach are filled with computer players as in any
match. A test that drives a `MatchState` itself calls `SimulateHuman` on a
human seat before the first planning step; the planner refuses a human seat
that is not marked. The mark lives only in that `MatchState`: a save, a clone
and a replay journal do not carry it, so the runner refuses `VerifyReplay` for
a match with simulated humans. A simulated human plays like a computer player,
so it provokes less than a person would; read results about aggression with
that in mind. The `ai-tournament` command does not expose the option.

A small, stable worker pool is expected. If a prior interrupted run left stale
workers, perform validation and then stop all .NET build servers owned by the
current user:

```powershell
./tools/Invoke-Validation.ps1 -ShutdownBuildServersAfterRun
```

That switch is intentionally not the default: it also stops build servers used
by an open IDE, making its next build colder. It does not stop the game. Avoid
running raw `dotnet build` and `dotnet test` commands concurrently in this
checkout; use the serialized entry point. If a running development game must be
preserved for a particular investigation, give that build its own explicit
`--artifacts-path` and accept the cold-build cost.

The manually dispatched continuous-integration workflow runs this verification
on Windows x64, Linux x64, macOS arm64, and macOS x64. It also publishes with
`IncludeOriginalAssets=false`, rejects any resulting `Assets` directory, and
runs the published `--smoke-test` entry point without an original asset pack.
Disposable installer artifacts from this workflow are retained for one day so routine validation
does not consume the repository's Actions storage for the default multi-month retention window.
After all four platforms pass, its Windows packaging job builds the clean-room
self-contained package and installer, installs with `/NOIMPORT=1`, launches the
packaged `--platform-smoke-test` entry point, uninstalls it, and uploads both
artifacts. The Windows publisher and installer gate require adjacent SDL2 and
OpenAL libraries so a metadata-only smoke test cannot mask a real launch
failure. Ordinary pushes do not dispatch this workflow.

`Verify-Repository.ps1` applies `tools/repository-policy.json` to Git-tracked
files. It rejects extracted/imported roots, original-media extensions outside
explicit clean-room or synthetic fixture roots, decompiler, disassembly and
analysis-database artifacts anywhere (Ghidra `.gpr` projects, `.rep`
directories and `.lock` files, `.gzf`/`.gar`/`.gdt` archives, IDA
`.idb`/`.i64`/`.id0`-`.id2`/`.nam`/`.til` databases, Binary Ninja `.bndb`, and
`.lst`/`.asm` listings), and unreviewed files larger than 1 MiB. The Windows publisher invokes the same check before deleting or
creating package output.

Runtime-diagnostics tests open an isolated log directory, deserialize the
JSON-lines lifecycle stream, verify stable event ordering, and check that
unique crash reports link back to their session log. They also inspect the
bounded ZIP export, prove repeat exports cannot overwrite one another, verify
that only allowlisted structured fields survive, and ensure raw exception
messages and filesystem paths do not enter crash summaries or exported session
events. Manual crash validation
should additionally confirm that `%LOCALAPPDATA%\ChaosOverlordsNewChrome\Logs`
retains at most five session logs and ten crash reports and that an unwritable
directory never prevents startup.

Persistence recovery tests corrupt and remove current save and replay generations,
verify fallback hashes against the last valid backup, reload the repaired primary,
check temporary-file cleanup, and confirm that a backup-only save slot remains
discoverable in the client browser.

Online-session integration tests restart against an advanced match both with and
without a prior snapshot, replay intervening sealed turns, restore the caller's
current submission and readiness, and verify that the live stream resumes from
the refreshed event sequence before resolving the next turn.

The first complete hosted run of this matrix and installer path was GitHub
Actions run `34400362789` on 2026-09-09. The latest recorded clean-room matrix
and installer run is `34403047147`, with zizmor run `34403047115`; every
selected platform, packaging, and audit job passed. Hosted runs remain the
authority for runner-specific compatibility.

Linux and macOS installer builders run only on matching native hosted runners.
The Linux gate opens the generated `.deb` and smoke-runs its installed-layout
executable. Each macOS gate validates the generated plist, smoke-runs the app
bundle executable, builds the `.pkg`, expands it again, and confirms the game
payload. Windows additionally exercises silent install and uninstall. The
manual release gate signs and verifies Windows executables and the installer
through SSL.com eSigner, and signs the Linux `.deb` with a detached OpenPGP
signature after checking the signing configuration up front and before building;
that signature is verified against the expected key fingerprint, and rejected if
the key has been revoked or has expired, before upload;
local and continuous-integration packages remain unsigned, and macOS signing and
notarization remain deliberately out of scope.

GitHub Actions dependencies are pinned to immutable commits corresponding to
their documented latest releases. `.github/workflows/zizmor.yml` uses the
official zizmor action in non-Advanced-Security auditor mode, causing any
finding to fail its pull-request check. It also supports manual dispatch and
does not run on pushes.

Validate a legal original installation without writing anything:

```powershell
dotnet run --project src/Rechaos.Extractor -- --verify-source `
  --source "C:\GOG Games\Chaos Overlords"
```

Fully rehash an installed output pack:

```powershell
dotnet run --project src/Rechaos.Extractor -- --verify-output `
  --output src/Rechaos.Game/Assets
```

`--quick` skips content hashes and checks manifest structure, safe paths,
presence, and lengths only. It is a startup optimization, never parity evidence.

Add `--json` to `--verify-output` for a versioned automation report on standard
output. Schema version 1 includes the absolute asset root, quick/full mode,
expected and manifest format/counts, verified-file count, and stable diagnostic
code/message/path/expected/actual records. Success and failure retain exit codes 0 and 1.
Examples include `asset_missing`, `asset_size_mismatch`, `asset_hash_mismatch`,
`unexpected_asset`, and `manifest_missing`.

Generate the asset catalog only from a pack that passes full verification:

```powershell
dotnet run --project src/Rechaos.Extractor -- --catalog `
  --output src/Rechaos.Game/Assets `
  --catalog-output docs/ASSET-CATALOG.md
```

Re-run the full paired-pixel RGB555/RGB565 comparison:

```powershell
dotnet run --project src/Rechaos.Extractor -- --analyze-px `
  --output src/Rechaos.Game/Assets
```

Compare two sanitized mechanical-state captures:

```powershell
dotnet run --project src/Rechaos.Tools -- state-diff `
  expected-state.json actual-state.json `
  --labels docs/schemas/state-labels.example.json
```

The fixture contract is
[`schemas/reference-fixture.schema.json`](schemas/reference-fixture.schema.json).
It pins the executable and source-pack hashes, experiment/finding identity,
initial and final state, commands, expected events, and phase-boundary hashes.
Only sanitized mechanical values belong in a checked-in fixture; original
pixels, media, saves of uncertain redistribution status, and narrative text do
not.

## Current canonical identities

- Full `DATA` + `HELP` + `MUSIC` source fingerprint:
  `ad958a934a691318f31a27a87252f420dd89a0ad03759457d8feaf49914d29e3`
- Bundled gameplay JSON:
  `e65f80e4d9a99ceeffbbc7fb335ef7f57b368ef87c1c56af23bcd759cd4e8b3a`

The bundled JSON is pinned to LF checkout bytes in `.gitattributes`. Its hash
is byte-level provenance, so platform newline conversion is not permitted.
- Original manual:
  `bdb1072848df95111cd014faaa7297d016b7c6e55cd7f2658dda67a167a0089d`

Individual gameplay table hashes are in `GameplayDataProvenance` and the format
log. The current installed pack contains 686 manifest entries representing 471
original source resources; 214 entries are decoded PX08 derivatives retained
alongside their original inputs, and one entry is the local modern help
document decoded from the two original WinHelp resources.

## Experiments on the original

An experiment is a controlled run of the original, recorded as an `EXP-` entry
in `spec/experiments/` with a JSON fixture next to it, in the form the
[documentation standard](https://dinorefurb.com/documentation-standard/#experiments)
sets out. It starts from a saved state, usually a save patch, changes one
input, and records what follows. It is repeated from the same state with the
random number generator's state varied between runs, and anything random gets
enough repetitions for a recorded distribution: a formula inferred from one roll
is a guess. Run the original offline. The experiments still to run are listed in
[manual_validation_plan.md](../manual_validation_plan.md).

For a manually operated Windows session, use
[`Capture-OriginalWindow.ps1`](../tools/Capture-OriginalWindow.ps1) and follow
the raw-burst evidence rules in [REFERENCE-CAPTURE.md](REFERENCE-CAPTURE.md).
The helper captures the visible desktop client area because the legacy
DirectDraw window may not produce reliable window-only captures on modern
systems. A capture that a test compares with the rebuild pixel for pixel has to
be taken at the screen entry's `resolution` (640x480), with no scaling,
filtering or aspect correction, and in the colours the game set in its palette.
The finding or experiment that cites a capture says which tool took it and with
what settings, and gives its xxh3. Captures, saves and recordings that hold any
of the game's content are never committed.

### The probe

`tools/Rechaos.OriginalProbe` runs the installed original under the Windows
debugging interface and records a new local game without anyone at the
keyboard. It checks the executable's SHA-256 against BLD-GOG-EN-1.1 first.

The GOG install registers compatibility layers for the installed executable's
path, among them RUNASADMIN, so starting that file needs an elevated prompt.
A copy at another path escapes them. Put `Chaos Overlords.exe` and
`SMACKW32.DLL` in a directory outside the repository, add directory junctions
named `DATA`, `MUSIC` and `HELP` that point into the install (the game finds
its data next to its own executable), set the other layers for the child
process, and pass the copy with `--executable`:

```powershell
$env:__COMPAT_LAYER = 'DWM8And16BitMitigation WINXPSP2 DISABLEDWM 640X480 DISABLEDXMAXIMIZEDWINDOWEDMODE'
dotnet run --project tools/Rechaos.OriginalProbe -- new-game --executable <copy> --out <run directory> [--game <install directory>] [--timeout <seconds>] [--scenario <0-9>] [--mentality <0-3>] [--turns <26|52|104|208>] [--humans <slot[:modifier]>,...] [--end-turns <n>] [--seed <n>] [--dump-at-roll <n>] [--trace-calls <hex address>] [--orders <turn:slot:action:target:target_2:repeat>,...] [--hires <turn:offer slot:sector>,...] [--families <turn:player:slot:family>,...] [--raiders <turn:player>,...] [--finance <turn:sector>,...] [--search <turn:definition+definition...>,...] [--sound]
dotnet run --project tools/Rechaos.OriginalProbe -- extract --experiment <EXP ID> --out spec/experiments/<EXP ID>.json <run directory>...
```

`new-game` switches full screen off in memory, silences the game unless
`--sound` is given (it sets both volumes of the Options dialog, `effects_level`
and `music_level`, to 0 in memory with the flags RULE-AUDIO-003 derives from
them, so no effect, movie sound or music plays; nothing the rolls or the state
depend on reads them), ends the logos and intro movies
by holding `left_button_down` in memory (RULE-VIDEO-001 ends a movie only when
the button is held at one of its ticks, so a posted click is missed), presses
Begin, records the seed and every `roll` with its call site and result, and copies
the writable sections once the first planning phase waits for input.
EXP-SETUP-001 gives the breakpoints and the procedure. Without options, Begin
takes the settings the setup screen opens with (the registry's preferences).
The options write what the setup screen's controls would commit before Begin
is pressed: the scenario in the original's numbering, the Mentality, the time
limit, and the slots that hold humans, each optionally named with one of the
six name modifiers (`right_hands`, `visibility`, `hire_force`, `elite`,
`islands`, `cash`), which the probe reads from the running executable. With
several humans the recording stops at the first human's Ready card, before
its hire offers are drawn. `--end-turns` presses Done that many times with no
orders, each once the next planning phase waits for input, which the first
call of the planning time-limit test `0x0041BDD5` (FND-TIMER-003) after
`elapsed_turns` has moved on shows, with Warn if Idle Gangs and Detailed Combat switched off in memory so
nothing waits for input, and dumps the state at the planning phase that
follows the last one. The human's planning phase opens the Combat Results
panel (SCR-COMBAT-001) after a fight that involved its gangs, and the Last
Turn Events panel (SCR-EVENT-001) when it has reports, and waits in each; the
probe breaks on both handlers and presses Exit before the next Done, and presses
Done again if a press left the turn unmoved for 20 seconds. `--orders` writes
an order into a gang record of the first human before the Done press of the
given turn, counted from 1: the `action`, `target` and `target_2` bytes of
FMT-STATE-001, and for a recurring order `repeat_action` and `repeat_target`,
as the order screens write them (RULE-TURN-005). The fixture lists each order
as an `order` input before its Done press, and the replay submits the same
order as a command. `--hires` writes the sector byte of the first human's
hire order for an offer slot into `hire_orders` before the Done press of the
given turn, as the hire screen does (RULE-HIRE-003); the fixture lists it as a
`hire` input, and the replay hires the gang the rebuild offers in that slot.
`--families` writes the `family` byte of a computer player's planning record
(FMT-STATE-007) and `--raiders` sets the player's byte of `raider_mode`
(RULE-AI-027), both before the Done press of the given turn, to reach families
no local match assigns. The fixture lists each as a `planning` input, and the
replay makes the same change to the rebuild's planning state; since that change
bypasses the replay recorder, such a run's journal is not verified.
`--search` sets the first human's `search_filters` entries for the given site
definitions before the Done press of the given turn, as the Search panel's
rows do (RULE-SEARCH-001), and keeps the site markers of each city redraw
(FND-SEARCH-006). The fixture lists each write as a `search` input and holds
the markers of the last redraw before the dump as `city_markers`; the replay
compares them with the rebuild's markers for the same filter.
`--finance` opens the Financial panel before the Done press of the given turn,
once that turn's orders and hires are written: the City variant for sector -1,
otherwise the Sector variant, after writing the sector into the map selection.
It presses the part of the console's Financial control that opens the variant
(SCR-UI-003), keeps the nine numbers the panel draws (FND-FINANCE-003) and the
sector the panel function was passed, and presses the panel's close control.
The fixture lists each opening as a `left_click` input before the Done press
and the run's panels under `finance`; the replay compares them with the
rebuild's projection of the same panel.
Each run records the roll count at every press as `done_at_roll`. `--seed` writes the given value over the argument of `srand`, so
a recorded run can be played again, and `--dump-at-roll` copies the writable
sections and the top of the stack at the entry of that call of `roll`, counted
from 0, into
`at-roll-<n>` in the run directory, to look at the state that led to a
divergence. `--trace-calls` sets a breakpoint on a function of the original and
adds a note to `trace.json` for each call: the roll count so far, the calling
instruction, the first four stack arguments and the returned value. Comparing
those notes with the same calls in the rebuild shows which call first gave a
different answer. `extract` refuses runs recorded with different
settings, since the runs of one experiment differ only in the seed. The run
directory holds
the original's memory and never goes into the repository. `extract` reads the
numbers of the spec's state layouts and glossary terms out of one or more run
directories and writes them as the runs of an experiment fixture, with no
names or texts. Among them are each player's Last Turn reports of the last
resolution (FMT-STATE-006), which the first run of EXP-TURN-001 and every run
from EXP-TURN-010 on hold, apart from the traced second run of EXP-TURN-021.
The same runs also hold each player's running totals (`cash_earned`,
`cash_spent`, `damage_inflicted`, `casualties`, `overthrow_count`,
`hide_count`) and their `hire_role` and `previous_hire_role`; the replay
compares them only in the runs that hold them, and reads the -1 the original
keeps in a human player's `hire_role` as the rebuild's 0. The fixtures also hold
the `scenario_score` and `scenario_standing` the last evaluation stored. A run
that ends the match stops when the endgame draws the awards, and its fixture
holds `match_over` and each player's first three `player_awards` entries.
`OriginalNewGameExperimentTests` replays every run of the EXP-SETUP and
EXP-TURN fixtures against the rebuild and names the first roll whose bound or
result differs, with the original's call instruction, then compares the state
and, where the fixture has them, the reports.

## Static binary research

`tools/ghidra/` holds bounded, clean-room Ghidra scripts for navigating the
owned executable, and [GHIDRA.md](GHIDRA.md) documents the headless workflow.
Fingerprint the executable before analysis, work from facts (constants, data
references, branches, state offsets and call relationships), keep neutral names
until behaviour confirms a meaning, and write each result up as a finding in
`spec/findings/` with its addresses, tool version and a way to find the place
again. Decompiler line numbers are never locations. Decompiler output, raw
analysis databases and executable material stay outside the repository. The
static work still open is listed in
[static_validation_plan.md](../static_validation_plan.md).

## Spec checks

`node tools/check-spec.mjs` runs the documentation standard's
[checks](https://dinorefurb.com/documentation-standard/#checks) over `spec/`,
`PARITY.md` and `DEVIATIONS.md`, and writes the indexes in `spec/index/`. It
also fails when a code comment gives an address that no entry the comment
cites records, in its locations or text or in the evidence of an entry it
cites. This is the address check of the toolkit's documentation check
(kibertoad/refurbished-dinosaurs-template#39), with the same rules: comments
are read from `.cs`, `.ts`, `.js` and `.mjs` files, so `//` inside a string or
a regular expression is not a comment and `/* … */` is; a neutral name (`fn_…`,
`g_…`) is always an address, and a plain `0x…` value is one only inside an
image given with `--images` (by default the executable's,
`0x00400000..0x004C9000`, from FND-EXE-001), so colours, masks and offsets are
left alone. A range larger than `--max-range` (64 KiB by default), such as a
whole section, records only its two ends, nothing inside it. When a comment
fails, cite the finding that records the address, or write one.
The fast gate runs it with `--check`, and `.githooks/pre-commit` runs it before
each commit once a clone enables the hook. The hook copies the index to a
temporary directory and checks that, so it judges what is being committed, not
unstaged edits. It compiles the Kaitai definitions when
`kaitai-struct-compiler` (or the path in `KSC`) is on the path and warns when
it is not. Until the patch tool is published with the standard's spec package,
an experiment that uses a save patch also gives each write as a byte offset and
a value in its Setup section.

## Tests against the original

`PARITY.md` lists, for each row, only the tests that compare the rebuild with
evidence from the original: decoding every file a format entry lists, replaying
an experiment fixture, or matching a capture. Decoder tests on synthetic files
and tests that compare the rebuild with an earlier version of itself are still
required but are left out of that column. Manual play never counts, and listed
tests run with every deviation that has a setting switched off. A `mandatory`
deviation cannot be switched off, so a listed test that reaches the behaviour it
changes cites the deviation's ID and leaves that case out or compares with the
original's result as the deviation changes it.

## Fixture classes

- **Format fixture:** synthetic non-copyrighted bytes testing parser boundaries.
- **Definition fixture:** checked-in mechanical values with pinned provenance.
- **Experiment fixture:** the JSON file of an `EXP-` entry: starting state,
  inputs, expected events by glossary name, expected end state, and for random
  outcomes the recorded distribution and its statistical test.
- **Command fixture:** initial state, input commands, expected events and hashes
  of the rebuild itself.
- **Visual fixture:** extracted locally and never committed; comparison metadata
  and masks may be committed.
- **Save patch:** writes to a base save, committed under
  `spec/experiments/saves/`; the base save itself stays with the maintainer's
  captures.

## Native audio backend

The native soundtrack EOF regression pins the old stream at its decoded end before
replacing its program, then requires the replacement decoder to reach the end of a
2.5-second synthetic track. This checks stale completion-state truncation, without
proving audible output or final partial-buffer delivery. The production adapter
binds MonoGame DesktopGL 3.8.5.1 `OggStreamer.Instance` and `pendingFinish` through
reflection: explicit stop removes and synchronizes with the old stream before the
flag is reset, and the new stream is published afterward. A paused resume retains
its existing completion state. Dependency upgrades must preserve these boundaries
or replace this hook with an equivalent supported API.

A separate native diagnostic confirms a remaining tail-delivery defect in the pinned
backend. Keep the initially prepared 0.5-second buffer playing with native looping,
seek the decoder of the synthetic 2.5-second track to 2.25 seconds, and wait for
`pendingFinish`. The decoder reaches 2.5 seconds, but `ALGetSourcei.BuffersQueued`
is 1 rather than 2: the decoded 0.25-second tail was not queued. The streaming
worker sets `finished` on that read and queues buffers only when `!finished`.
This diagnostic intentionally changes decoder position to isolate submission; it
does not compare audible hardware output. Whole-track decoding alone therefore
cannot establish the endpoint required by RULE-AUDIO-001. Repairing final-buffer
submission remains necessary before claiming full soundtrack parity.

## Native pattern fill reference

`NativePatternFillTests` compares the visible pixels from the production
pattern-fill helper with Windows `Rectangle` in a new memory DC, which holds the
default black pen, filled with a solid white brush and with a solid black brush,
the two fills the game uses (FND-GFX-006). It covers the current production
rectangle sizes, smaller examples and one-pixel-wide strips, without original
assets. A separate case records that Windows leaves the initialized scratch bitmap
of a 1-by-1 rectangle unchanged while the helper draws it black; no current game
caller fills that size, and the helper parity claim excludes it. The tests check
fill and outline pixels only. The compositor raster operations and screen
rendering are unverified. They skip outside Windows.

## Original pattern resources

`OriginalPatternResourceTests` reads the hash-verified original executable's bytes, walks its PE resource directory for bitmap resources 143, 146 and 147, checks their headers and black and white palettes, and compares all 192 mask bits with the production pattern helper (FND-UI-031, FND-GFX-006). It does not load or execute the original, so it runs on every platform, and it does not store resource bytes in the repository. It requires `GAME_DIR` and skips when the executable is unavailable. This verifies mask shape, palette and row orientation, not raster-operation compositing or rendered-screen parity.

## Failure triage

Classify mismatches as:

- source/version mismatch;
- decode/offset error;
- state initialization difference;
- command legality difference;
- resolution/order difference;
- RNG algorithm or consumption difference;
- presentation-only difference;
- manual-versus-binary discrepancy;
- intentional modernization leaking into compatibility mode.

Reduce a failure to the earliest mismatching phase hash. Preserve the smallest
replay and all source identities needed to reproduce it.

## First-planning map comparison without the keyboard footer

On 2026-10-02, the deterministic first-planning-entry capture of the
EXP-SETUP-001 state (seed 52421) at selection-marker frame 6 was compared over
map rectangle `(2,42,432,416)`. The original window capture and rebuild
differed in 5,763 RGB pixels. Of these, 5,088 had exact-white original pixels
and were classified as the Windows 11 white-block artifacts that FND-UI-041
notes in the city. All 675 remaining differences occurred at y 439 through
445, where the rebuild draws the mandatory keyboard footer (DEV-UI-023).

An external diagnostic build using the corrected console renderer omitted
only that footer draw. It produced zero differing nonwhite pixels over the
same map rectangle; the 5,088 original-white differences remained. The game
implementation retains its documented footer. Captures, diagnostic projects
and original memory remain outside Git.

This comparison supports the unaffected map pixels of one fixed state and
frame. Pixels that are exact white in the original capture were set aside as
capture artifacts, so the rebuild's sprites under them remain unchecked. It
does not establish all marker frames, every selected sector, search overlays,
pointer states or whole-screen parity.

## All active-player marker frames in the first planning view

On 2026-10-02, an external diagnostic copy of the original probe collected
all twelve active-player marker frames from the first planning view of the
EXP-SETUP-001 state (seed 52421), the state of the previous section.
FND-UI-038 steps the counter `0x00487B90` after drawing frame `k`, so a
capture taken while the counter holds `c` shows frame `(c + 11) mod 12`.
Each accepted frame had the same counter value before and after two
byte-identical non-repainting window captures. The probe pumped the original
between captures until its counter changed; it did not patch the executable
or synthesize marker artwork.

Twelve rebuild captures came from the external diagnostic harness of the
previous section, which selected frames 0 through 11 explicitly instead of
running the game's timer, and omitted the DEV-UI-023 footer below the compared
rectangle. Every capture matched the original over Overlord-bar rectangle
`(2,0,432,42)` with zero differing RGB pixels. This includes the occupied
portraits and the marker position for the viewed seat in this state.

This verifies frame artwork and placement for one viewer and state. It does
not measure timing, dropped ticks, focus behavior, other viewed seats, empty
seat animation or the two-frame selected-sector outline.
Captures and diagnostic code remain outside Git.

## Both selected-sector outline states

The twelve synchronized original captures above contain both selected-sector
outline states, the selection frames `f = 0` and `f = 1` of FND-UI-017. They
were compared over the map with DEV-UI-023's footer omitted in the external
diagnostic harness. Against the rebuild's frame 0, the original captures
labelled with active-player marker frames 2 through 7 have zero differing
nonwhite map pixels. The other six captures differ in exactly 200
nonwhite pixels, all inside border bounds x 219 through 270, y 97 through 146
of the selected cell at column 4, row 1.

A second external build selected frame 1 explicitly. It matches those six
captures with zero differing nonwhite map pixels, and differs from the first
six by the same 200 border pixels. Thus every original map capture matches
one of the two outline states outside exact-white artifact pixels. The
independent player-marker labels identify these particular captures; they do
not imply that the two animation counters share a phase.

This verifies both outline artworks and placement for the selected cell in
this state. The comparison does not measure their period, reset behavior,
other cells, dropped ticks or the pixels covered by the expected Windows 11
artifacts. The gang-status markers need no timing comparison: RULE-UI-006
picks each one from the sector's state, and no timer cycles them.

## Completed-state final calendar and report capture

An isolated capture from the actual completed EXP-TURN-042 replay exposed a
presentation error: its coordinator had moved to turn 27, while the original
final visit still held elapsed turns 25. Reading the coordinator directly drew
console week 27 and report week 26. A diagnostic that sets the turn to 26
directly does not exercise this completed state, so it cannot catch the error.

The city and Events renderers now derive presentation elapsed turns from the
outcome turn after completion, and from the coordinator during an active match.
The completed replay retains coordinator turn 27 and outcome turn 26; it draws
console week 26 and report week 25, matching FND-UI-041 and EXP-TURN-042.
Report projection continues to select resolution-turn 26 records.

The external renderer loaded the actual completed replay save without changing
its turn or outcome, selected player 0 and the Events panel, and froze updates.
With marker frame 8, compared with the paired original EXP-TURN-042 capture,
the console calendar rectangle `(481,15,101,7)` has zero differing RGB pixels.
The entire Events panel `(104,124,344,209)` also has zero differing RGB pixels
across all 71896 pixels, including date, subject, caption, controls and artwork.
This region has no expected Windows 11 white-block differences to exclude.

The diagnostic selected the viewer and panel directly; handler entry and closing
are tested separately. This comparison covers one cash-short report and final
state, not all reports, combat results, multiple viewers or input timing.
Screenshots, saves and the diagnostic harness remain outside Git.

## Final-entry hire dock comparison

In the EXP-TURN-042 original final-entry capture, all three 64-by-64 hire
portraits match the corresponding completed replay render exactly: zero
RGB differences in each of the 4096-pixel cells. The three retained offer
IDs also agree with the `hire_offers` values of the numeric original fixture,
so the rebuild needs no extra offer draw for this final visit.

The earlier layout's price strip `(438,436,198,24)` differed in 158 pixels,
confined to x 449 through 592 and y 440 through 446. Applying the existing
hire-price-origin correction from commit `f737f302` places the three prices
at x 450, 516 and 582, as FND-HIRE-007 records. Repeating the isolated render
then matches all 4752 pixels of that strip exactly. The calendar and entire
Events panel remain exact after this correction.

This validates the existing correction against a later completed-match state,
in addition to the first-planning layout tests. It does not establish every
hire/snub mark, other offer combinations or drag and release behavior. The
reference screenshots and diagnostic renderer remain outside Git.

## Completed-state local waiting-light comparison

FND-UI-043 establishes that completing a local human planning visit marks
that seat's orders as submitted and redraws its waiting light black. The
next round resets the flags; the final city visits retain the completed
round's flags. The renderer previously assumed only online play submitted
orders, leaving the local final-view light lit.

The external EXP-TURN-042 renderer was rebuilt with the committed city-map
renderer, retaining only its diagnostic marker-frame override. It loaded
the same actual completed replay save, selected player 0 and Events, froze
updates, and captured marker frame 8. Compared with the paired original,
the entire Overlord bar `(18,5,404,32)` matches in all 12928 RGB pixels.
The human waiting light `(51,30,20,6)` matches in all 120 pixels. No artifact
exclusion is needed for these regions. The console calendar, entire Events
panel, three hire portraits and hire-price strip remain exact.

The focused fast gate passed 39 tests, with no skips or build warnings or
errors, including actual rendering-predicate checks for initial planning,
an earlier completed local seat, a later waiting human, the next turn's
upkeep and reset, and the completed EXP-TURN-041 and EXP-TURN-042 states.
The hot-seat transition is a synthetic state checked against static
evidence; no original multi-human capture covers it.
The pixel comparison covers one final state and marker frame; later local
rounds, multiple original human viewers and input timing remain unverified.
Screenshots, saves and the isolated harness remain outside Git.
