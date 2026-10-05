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
- [Screens against captures of the original](#screens-against-captures-of-the-original)
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
dotnet run --project tools/Rechaos.OriginalProbe -- new-game --executable <copy> --out <run directory> [--game <install directory>] [--timeout <seconds>] [--scenario <0-9>] [--mentality <0-3>] [--turns <26|52|104|208>] [--humans <slot[:modifier]>,...] [--end-turns <n>] [--seed <n>] [--dump-at-roll <n>] [--trace-calls <hex address>] [--orders <turn:slot:action:target:target_2:repeat>,...] [--hires <turn:offer slot:sector>,...] [--families <turn:player:slot:family>,...] [--raiders <turn:player>,...] [--retire <turn:player>,...] [--finance <turn:sector>,...] [--search <turn:definition+definition...>,...] [--time-limit <0-3>] [--expire-turns <turn>,...] [--comlink <script file>] [--sound] [--capture] [--white-key] [--equip-lists] [--attack-lists] [--draw-values <hex address>=<int32>[/<int32>...],...] [--search-clicks <x:y>,...] [--hire-steps <drag:slot:sector|reject:slot|exit>,...] [--order-steps <open:sector|card:n:x:y:command|strip:x:y:command|back|exit>,...] [--gang-markers] [--pointer] [--sounds]
dotnet run --project tools/Rechaos.OriginalProbe -- extract --experiment <EXP ID> --out spec/experiments/<EXP ID>.json <run directory>... [--screens <SCR ID>,...]
dotnet run --project tools/Rechaos.OriginalProbe -- extract-comlink --experiment <EXP ID> --out spec/experiments/<EXP ID>.json <run directory>...
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
Done again if a press left the turn unmoved for 20 seconds. Each call of
either handler is kept with the roll count at the call and whether the panel
stayed open until the probe pressed Exit, since the Combat Results handler
returns at once when no fight qualifies; a panel still open at the dump counts
as shown. The fixture holds the calls as `panels`. `--orders` writes
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
(RULE-AI-027), and `--retire` clears the player's byte of `player_active`
(FND-STATE-004), all before the Done press of the given turn, to reach families
no local match assigns or a match that ends with one player active. The fixture
lists each as a `planning` input, and the replay makes the same change to the
rebuild's state, a retired player becoming eliminated with its gangs and
sectors left in place; since that change bypasses the replay recorder, such a
run's journal is not verified.
`--search` sets the first human's `search_filters` entries for the given site
definitions before the Done press of the given turn, as the Search panel's
rows do (RULE-SEARCH-001), and keeps the site markers of each city redraw
(FND-SEARCH-006). The fixture lists each write as a `search` input and holds
the markers of the last redraw before the dump as `city_markers`; the replay
compares them with the rebuild's markers for the same filter.
A run that ends the match keeps the endgame's first drawing: the renderer's
arguments and the player of each row it lists, ranked, eliminated or the
victory splash (FND-AWARDS-005), which the fixture holds as `endgame_rows`.
`--finance` opens the Financial panel before the Done press of the given turn,
once that turn's orders and hires are written: the City variant for sector -1,
otherwise the Sector variant, after writing the sector into the map selection.
It presses the part of the console's Financial control that opens the variant
(SCR-UI-003), keeps the nine numbers the panel draws (FND-FINANCE-003) and the
sector the panel function was passed, and presses the panel's close control.
The fixture lists each opening as a `left_click` input before the Done press
and the run's panels under `finance`; the replay compares them with the
rebuild's projection of the same panel.
`--time-limit` writes `planning_limit_choice` before Begin, and
`--expire-turns` leaves out the Done press of the listed turns so their
planning time runs out; the fixture lists each as a `wait` input and records
the planning clock of each such turn as `timers` (RULE-TIMER-002,
RULE-TIMER-003).
`--equip-lists` reads the item lists of the Equip panel after the dump: at
the next `PeekMessageA` call of the message pump (FND-UI-020) the probe saves
the thread context and calls the list builder `fn_0043F136` (FND-EQUIP-008)
for each category of each living gang of the first human, with the Tech Level
of the gang's definition, as the panel does. The builder's research test reads
`active_player`, so the probe sets it to that human for the calls and puts it
back with the context afterwards. The
fixture holds the items of each list, in entry order, as `equip_lists`; the
replay compares them with the rebuild's legal Equip orders of the gang in that
category (RULE-EQUIP-004).
`--attack-lists` does the same with the Attack picker's roster builder
`fn_0043D132` (FND-ATTACK-006), for each other player and each living gang of
the first human, with the gang's sector. The builder tests what `active_player`
sees, so the probe sets it in the same way. The fixture holds the roster slots
of each list as `attack_lists`; the replay compares them with the gangs the
rebuild's Attack picker shows for that opponent (RULE-ATTACK-002). Neither
fixture names the player, and the replay takes the lowest human slot, so the
probe refuses `--equip-lists` and `--attack-lists` when the first `--humans`
slot is not the lowest.
`--search-clicks` posts a left-button press and release at each client point
after the dump, lets the original run for half a second after each, and keeps
the whole `search_filters` table, the active player and whether the Search
handler `fn_00448E32` is running (FND-SEARCH-001, FND-SEARCH-002). The fixture
holds them as `search_clicks`; the replay passes each point to the rebuild's
console and Search panel hit tests and compares the tables (RULE-SEARCH-001).
`--hire-steps` works the Hire dock after the dump: `reject:s` clicks offer
slot `s`'s Reject cross, `drag:s:sector` presses on the offer's portrait and
releases over the sector's city map cell, and `exit` presses a result panel's
Exit, skipped when no panel is open. A step that makes the original roll ends
the run as not dumped. The Hire handler follows the two pointer points the
window procedure keeps (FND-UI-020), one of them taken from the desktop
cursor, so a drag writes both points itself, again just before the release,
and posts only the button messages. The probe keeps `hire_orders` after each
step as `hire_steps`, and keeps the panel calls as they stood at the dump; the
replay passes each press and release point to the rebuild's dock and city map
hit tests, takes each step through the rebuild's dock and compares the orders
(RULE-HIRE-003).
`--order-steps` works the detailed sector screen after the dump: `open:n`
double-clicks city sector `n`, `card:n:x:y:command` presses card `n` at
`(x, y)` within the card, `strip:x:y:command` presses the window at `(x, y)`,
and `back` and `exit` press the back control and a result panel's Exit; an
`exit` with no result panel open is skipped. A
press that opens an order popup reaches the popup helper's `TrackPopupMenu`
call (FND-UI-021); the probe keeps the menu and the command and greyed state of
each item, then skips the call and hands the helper `command`, 0 for no choice,
so no menu is shown (EXP-TURN-095). The probe keeps the menu, the view, the
player whose gangs the cards list (EXP-TURN-096), the card slots and the
active player's order bytes after each step as
`order_steps`; the replay takes each step through the rebuild's strip hit
tests, its order panel and its orders and compares them (RULE-TURN-005), and
takes each press on an Overlord portrait through the rebuild's portrait
handling and compares the cards (RULE-UI-010).
`--gang-markers` logs every gang-status marker the original draws: each full
city redraw, each frame drawn for a sector holding the player's gang, each copy
of the saved cell back and each incoming-only mark (FND-UI-024, EXP-UI-004),
from the last full redraw before the dump on, tagged with the hire or order
step it came in. The fixture holds them as `gang_markers`; the replay takes
the steps through the rebuild's marker map and compares the map after each
(RULE-UI-006).
`--pointer` logs every call of the cursor helper `fn_00465BC8` (FND-UI-034)
with the rolls and Done presses before it, its shape and force and the address
of the call. The fixture holds them as `pointer_calls`; the replay checks that
every roll from the setup's hourglass on is made under the hourglass and
compares the rebuild's pointer at each planning entry and after each Done press
(RULE-UI-007, EXP-UI-022).
`--sounds` logs every call of the play helper `fn_0045851A` (FND-AUDIO-006)
with the rolls and Done presses before it, its slot and the address of the
call; with `--sound` the effects wrapper's calls are logged too. The fixture
holds them as `sound_calls` (RULE-AUDIO-006, EXP-AUDIO-001).
`--draw-values` writes 32-bit values into memory each time the planning-entry
function `fn_0046FD80` starts to draw the console (FND-UI-040): the nth value
at its nth call and the last at every later one. It makes the console draw a
number the match would not reach over what an earlier entry drew, as
EXP-UI-002 does with the score and cash. The calls are counted over every
human's planning entries and each human's console draws its own slot, so the
probe refuses `--draw-values` with more than one `--humans` slot, and it
refuses an address outside the executable's writable sections. The fixture
lists each as a `setup` input; the values change the match, so such a run is
not replayed.
Each run records the roll count at every press as `done_at_roll`, and as
`rolls_at_dump` the count the state dump follows when steps after the dump made
more, as a Ready press that refills the offers does (RULE-SETUP-008); the
replay counts draws up to it. `--seed` writes the given value over the argument of `srand`, so
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
From EXP-TURN-048 on, the fixtures also hold the computer players' planning
state: the planning record fields (FMT-STATE-007), `ai_started`,
`raider_mode`, `placement_anchor`, `sector_weight` and the `focus` and
`coverage_sector` values of the auxiliary records (FND-AI-044), each left out
when it holds 0; every combat record a resolution has written
(FMT-STATE-003); and the entries of the combat result rows that hold a gang,
with the `police_hit` values that are not 0 (FMT-STATE-008). The replay
compares the planning records byte for byte, and the auxiliary values only
for computer gangs whose family is assigned, since the original leaves the
others 0 from the start of a match where the rebuild holds -1. It rebuilds
the combat records and result rows of the last resolution from its attack and
police events, and compares the records of the gangs that fought,
`police_damage` in all 486 records and every result row.
`OriginalNewGameExperimentTests` replays every run of the EXP-SETUP and
EXP-TURN fixtures against the rebuild and names the first roll whose bound or
result differs, with the original's call instruction, then compares the state
and, where the fixture has them, the reports.

`--comlink` replaces the Done presses with a script of Comlink steps, one per
line, for a match with several humans (EXP-COMLINK-001). `visit p` waits for
player p's handoff card, presses Ready and closes the planning entry panels;
`view` and `send` press the two parts of the console's Comlink control;
`next`, `prev` and `dismiss` press the View panel's controls; `card p`,
`press send` and `press cancel` press the Send panel's; `type` posts a
`WM_KEYDOWN` for each character, with `{BACK}`, `{ENTER}`, `{LEFT}`, `{UP}`,
`{RIGHT}`, `{DOWN}` and `{EXEC}` for those keys; `dump` keeps the state; and
`done` presses Done. The probe also switches Slide Panels off in memory, so
a panel takes presses as soon as its handler runs. After each step it keeps
the panels open, the effect slots played, each message View showed with the
cursor and the numbers drawn, each drop of read messages at the end of a
player's planning, the Send panel's selection and draft, and every player's
Comlink counts, cursors, pending flag and message records (FMT-STATE-005).
`extract-comlink` writes those as the steps of a fixture, and
`OriginalComlinkExperimentTests` plays the same steps in the rebuild and
compares them after each one.

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

## Screens against captures of the original

A screen entry is compared with the original through a capture of the drawing
area taken at the endpoint of an experiment run. The rebuild replays the same
run, draws its endpoint, and has to draw every listed element of the screen as
the original did.

### Taking a capture

Add `--capture` to the probe's `new-game` command. After the state dump the
probe copies the 640-by-460 drawing area (RULE-GFX-002) from the window's
device context twice with BitBlt, without asking the window to repaint, and
reads the Overlord bar's marker counter `0x00487B90` before and after
(FND-UI-038). It retries until the counter held still and the two copies agree
byte for byte, together with the pump's counter `0x00487804`, then notes the
marker frame the copies show, `(c + 11) mod 12` for a counter value `c`, and
writes `capture-blt.bmp` and
`capture-blt-repeat.bmp`, top-down 32-bit bitmaps, into the run directory. It
also notes the original's display depth `0x0048787C` (FND-PLATFORM-009) and the
depth of its own device context; a capture whose depths differ is recorded as
such. PrintWindow is not used: the timer draws the marker straight to the
window, and a repainting copy loses it.

Add `--white-key` as well (docs/DECISIONS.md, 2026-10-05). On a 32-bit desktop
the original's keyed copies key nothing and draw the white they should leave
out, which is where the solid white areas of Windows 11 come from
(FND-PLATFORM-014). The option puts a breakpoint on the `SetBkColor` call of the
keyed mask compositor and, whenever that call passes the 16-bit key
`RGB(255,252,255)`, writes `RGB(255,255,255)`, the white a 32-bit surface holds,
over the argument. The fixture lists it as the setup input `key_colour
RGB(255,255,255)`. The rolls and the state of a run do not depend on it.
The breakpoint stops the original on every keyed copy, and the runs that
checked the option had no planning time limit; whether it moves the timer
records of a run with `--time-limit` has not been checked.

`extract --screens SCR-UI-003,SCR-HIRE-002` adds a `capture` object to each
run whose two copies agree:

- `xxh3`: the hash of `capture-blt.bmp`, the spec's xxh3 (`SpecHash`);
- `area`: `[0, 0, 640, 460]`;
- `marker_frame`: the marker frame the capture shows;
- `pump_counter`: the value of the pump's counter `0x00487804`, which held
  still across both copies as well. The pump draws the selected-sector frame
  for its counter and then advances it, so the frame on screen is
  `((n + 7) % 8) / 4` for a counter `n` (FND-UI-017, FND-UI-048);
- `lamps`: the Events light's flag `0x00487814` and the byte `0x00487818` the
  pump sets when it draws that lamp, then the Comlink light's `0x0048781C` and
  `0x00487820` (FND-EVENT-006), read with the copies. Captures taken before
  the probe read them have none;
- `screens`: for each screen named, its elements, each with the element's name
  as the entry's Drawn elements table gives it, its `rect` `[x, y, width,
  height]`, the `xxh3` of the rectangle's pixels and `white`, the number of
  those pixels that are exact white.

The digest of a rectangle is the xxh3 of its pixels as red, green and blue
bytes, row by row from the top and left to right. The elements of a screen and
their rectangles, worked out for the state the capture shows, are in
`tools/Rechaos.OriginalProbe/Screens/<SCR ID>.json`; a screen with no such file
cannot be named yet. Each rectangle comes from the entry's Position column, and
an element whose position the entry does not give is left out of the list.
`ScreenElementListTests` checks every list: it names its screen entry, the
screen has an entry in `ScreenCaptureMasks`, each rectangle lies inside the
640-by-460 drawing area, and each element names a row of the entry's Drawn
elements table, or is named after the entry's title and covers the whole
drawing area. The name is the row's own, or the row's name (or that name's
part before its own comma) followed by a comma and either an index such as
`slot 0` or a field or variant that the row's Element or Shows cell names as a
whole word: `Offer portrait, slot 0` cites the row
`Offer portrait, one per offer slot`, and `Value fields, Gang Upkeep` cites
`Value fields`, whose Shows cell lists Gang Upkeep.

The bitmap holds the game's art, so it never goes into the repository. When
`GAME_DIR` is set, `extract` copies it to `GAME_DIR/captures/<xxh3>`, the
directory `OriginalGameFiles` reads captures from; otherwise it prints where to
copy it. Every file there is named by its xxh3 alone, with no extension, as the
documentation standard names them, and `OriginalGameFiles` refuses a copy kept
under another name such as `<xxh3>.png` instead of skipping the test.

Captures are taken without a DirectDraw wrapper (docs/DECISIONS.md,
2026-10-05). DDrawCompat beside the staged copy left the rolls and the state of
a recorded run unchanged but did not remove the white areas: windowed, the
original draws with GDI and never uses DirectDraw (FND-GFX-004).

A capture can also be taken after the dump, as a step of `--order-steps`:
`shot:SCR-A+SCR-B` copies the drawing area as `--capture` does and keeps
`capture-step-<n>.bmp` and its repeat, where `n` is the step's index, with the
marker frame, the pump's counter, the light bytes (`lamps`), the selected
sector `0x004ABC80` (`selected_sector`) and the frame counter
(`frame_counter`). While a panel that slid in is open the pump draws no
selection frame (FND-UI-051), so the probe breaks at `0x004196E4`, where the
slide-in sets the flag that stops it, and keeps the pump's counter read there;
the frame counter is that value while the flag is set, the pump counter when it
is clear, and null when the probe could not tell. A panel that slides in over
another finds the flag set and leaves the counter as it was. While Item
Information, Sell or Give is open the shot also keeps `item_frame`, the frame
of its rotating items, read from the handler's local before and after the
capture, which is taken again when the two reads differ (FND-UI-052,
FND-UI-053); while the idle gang warning is open, the ticks since its open
modulo 8, read from its countdown and shown flag (FND-UI-054); and for a shot
of the Comlink Send panel, 3 while its caret is drawn inverse and 0 while
plain, read from the byte at `0x00498110` (FND-COMLINK-010). A `warn` step
switches Warn if Idle Gangs back on for the steps after it. `extract` gives that
order step a `capture` object as above and a `screens` string naming the
screens it is compared at. The steps before it bring the screen up: `open:s`
double-clicks sector `s` on the city map, `dbl:x:y` double-clicks the window
point `(x, y)` as `open` does (FND-UI-020), `strip:x:y:0` presses a point,
`card` a sector card's point, `back` the detailed sector screen's back
control and `exit` the Exit of the panel the planning entry left open.
EXP-UI-006 to EXP-UI-014 are taken this way.

`new-game --title-capture` copies the title screen before the run presses New
Game: a breakpoint at the title loop's first load of its art (FND-UI-055)
stops the presses, and the drawing area is copied two seconds later. `extract`
gives the run a `title_capture` object, compared at SCR-UI-001, with marker
frame 0. `--credits-capture` then posts Help, About, copies the credits once
the breakpoint after their load (FND-UI-055) has been hit, and closes them with
the space bar; `extract` gives the run a `credits_capture` object, compared at
SCR-UI-002. `--setup-capture` writes the initialized values of the objective,
Mentality and planning limit options (FND-OPTIONS-001) before New Game, so the
setup screen opens as it does when the registry key holds none, and copies the
setup screen two seconds after it opens, before the run writes its own
settings; `extract` gives the run a `setup_capture` object, compared at
SCR-SETUP-001. `--setup-steps` then posts presses on the setup screen, as
`strip:x:y`, drags as `drag:x:y:x2:y2` and copies as `shot`, each copy
compared at SCR-SETUP-001; `extract` lists them as the run's `setup_steps` and
as `setup` inputs. A drag writes the two pointer points of FND-UI-020 at the
press, at each of eight steps to the release point and before the release, as
the hire steps do, and posts only the button messages. The run's own settings
then replace only what they set: any other choice keeps what the presses left,
and the trace notes which choices the presses changed. EXP-UI-015 is taken this
way, with an earlier drag that posted the moves as `WM_MOUSEMOVE` instead.

A capture recorded before the element digests existed, such as those of
EXP-TURN-041 and EXP-TURN-042, gets them from its bitmap under
`GAME_DIR/captures/` without another run of the original:

```powershell
$env:GAME_DIR = 'D:\original-files'
dotnet run --project tools/Rechaos.OriginalProbe -- digest --fixture spec/experiments/<EXP ID>.json --run <n> --screens <SCR ID>,...
```

### Comparing

`ScreenCaptureTests.TheRebuildDrawsWhatTheOriginalDrew` runs once for each
capture the experiment fixtures record. It replays the run with
`OriginalNewGameExperimentTests.ReplayedMatch`, writes the endpoint as a native
save, and starts the game with

```text
Rechaos.Game --assets <pack> --reference-frame <save> <bitmap> --marker-frame <n>
    [--pump-counter <0-7>] [--selected-sector <0-63>] [--lamps <0|1>,<0|1>]
    [--item-frame <0-14>] [--clip-tick <0-21>]
    [--reference-clicks <x:y[:2]|x:y>x:y|'TEXT>,...]
```

which shows the save at its planning entry in a 640-by-460 window, holds the
presentation clock at zero, draws three frames and writes the third as a
bitmap before it exits. `--pump-counter` passes the capture's `frame_counter`, or its
`pump_counter` when the fixture has none, which picks the selected-sector frame
drawn (FND-UI-048). `--selected-sector` passes `selected_sector`, which the
planning entry selects in place of the sector the rebuild keeps for the player
(FND-SAVE-003); a save holds no selection (DEV-SAVE-001). `--lamps` passes the
second and fourth of the capture's `lamps`, the bytes that say the Events and
the Comlink lamp were drawn lit, which pick the blink phase of those lights in
place of the clock's (FND-EVENT-006). `--item-frame` passes `item_frame`, the
frame the rotating items of Item Information, Sell and Give are drawn at
(FND-UI-052, FND-UI-053), the idle gang warning's ticks since its open
modulo 8 (FND-UI-054), or the Comlink caret's phase (FND-COMLINK-010).
`--clip-tick` passes `clip_tick`, the tick of the Detailed Combat clip a shot
shows (FND-COMBAT-016), which the clip the clicks started is drawn at. The blinking
and cycling parts of the screen stay at time zero however many clicks were
made: the marker is drawn at `--marker-frame`, or at its first frame without it.
`--reference-clicks` lists the presses that take the rebuild from the planning
entry to a shot step's screen, `:2` marking a double-click, and `'TEXT` text
typed into the Comlink Send panel a character at a time. They run on a clock
of their own, one button edge every 50 ms, wait while a pressed face or a flash
holds the input (RULE-TIMER-004), and the frame is drawn 20 updates after the
last one. Panels are drawn in place, without the slide. For a shot step the test
works the presses out from the order steps before it: a double-click at the
centre of the opened sector's cell for `open`, the step's point for `dbl` and
`strip`, a card's point for `card`, `(20, 425)` for `back` and the step's text
for `type`. It leaves out `exit`, since the reference frame does not draw the
planning entry's panels, and `wait`.
For a `title_capture`, `credits_capture` or `setup_capture` the test passes
`title`, `credits` or `setup` in place of the save; the game draws its title
screen, the credits over it, or the local setup as New Game first opens it,
without a match. A setup step's copy passes the presses before it as
`--reference-clicks`, a drag as `x:y>x2:y2`, which the rebuild makes as a
press, a move with the button down and a release.
The rebuild's orders are a panel (DEV-UI-021): a `card` step whose menu 1
choice runs a picker (Attack, Equip, Give, Influence, Move, Research or Sell,
FND-UI-021) becomes the card press and a press on that order's row of the
panel. Any other step that opened a popup menu makes the capture
unreplayable, and the test skips it. With `RECHAOS_KEEP_FRAMES` set to a directory, the test copies
each frame the rebuild drew there as `<experiment>-<run>-<step>.bmp`, step -1
being the endpoint, and as `<experiment>-<run>-<screen>.bmp` for a screen before the match. Preferences, saves and logs of that run go to a
`rechaos-reference-frame-*` directory beside the bitmap, never to the player's.
The test then compares each element:

- An element the original drew wholly in exact white is unverified: in a
  capture taken without `--white-key`, a keyed copy on Windows 11 draws solid
  white where its image should show through (FND-PLATFORM-014), and what
  belongs there is unknown.
- With the capture under `GAME_DIR/captures/`, every pixel outside the masks is
  compared. A pixel the original drew exact white is counted as unverified
  unless the rebuild drew it white as well. The element matches when no
  compared pixel differs.
- Without the capture, an element with no white pixel and no mask is compared
  by its digest, and any other element is unverified.
- A fixture whose inputs hold the setup input `key_colour`, written by
  `--white-key`, has no white left by a keyed copy, so its exact white is
  compared like any other colour: a pixel the original drew white and the
  rebuild did not differs, and without the capture an element with white
  pixels and no mask is compared by its digest. EXP-UI-003 is compared this
  way.
- `ScreenCaptureMasks` lists, for each screen, the rectangles a deviation draws
  over, each under the ID of the deviation. All the masks of the screens a
  capture names apply to the whole frame. `EveryMaskCitesADeviationFromItsScreen`
  checks that each deviation's Departs from line names the screen.

The test prints every element's verdict and fails on an element that differs.
It skips a capture that records no screen elements, and skips the rendering
when no asset pack is installed: the gate builds with
`IncludeOriginalAssets=false`, so it finds the pack only in the player's
application data (`ChaosOverlordsNewChrome/Assets`). A plain
`dotnet test --project tests/Rechaos.Tests` in a checkout with
`src/Rechaos.Game/Assets` finds it beside the test binary.
`OnlyTheMarkerFrameChangesAReplayedEndpoint` checks the reference frame itself:
two renders of the EXP-SETUP-001 endpoint at marker frames 6 and 0 differ only
inside the marker.

A screen row of `PARITY.md` lists `ScreenCaptureTests` once a capture of the
original covers its elements and they match; the elements left unverified are
named in its Notes.

### What the reference frame shows

The reference frame starts at the city screen and its console (SCR-UI-003,
SCR-HIRE-002) of the player whose planning entry the run ends at, with no
pointer, and then makes the scripted presses. With several local humans it
starts at the hand-off card (SCR-SETUP-002) and a press of its Ready goes on as
in play; for a run whose match ended it starts at the endgame (SCR-AWARDS-001),
and for one whose last resolution eliminated a local human at that player's elimination card
(SCR-OBJECTIVE-002). A run stops at the elimination card as it stops at the
endgame.
It does not draw Combat Results or Last Turn Events that the planning entry
would open first, but it closes Last Turn Events as a press of its Exit after
the first page would: the Events light stays on only while the player has another report
to see (RULE-EVENT-005). EXP-UI-007's capture, after the original's planning
entry showed its one report and the Exit closed the panel, has the light's
flag clear. A capture taken with Combat Results or Last Turn Events open, at
the final view, during a drag or with a popup menu open cannot be compared.

The selected-sector outline cycles through two frames on the pump's counter
(FND-UI-017), and the reference frame draws the one the capture's
`pump_counter` gives (FND-UI-048); without one it draws the first. It draws the
Events and Comlink lights in the lit half of their blink whenever they are on.
No capture yet shows a light lit, so which counter values the lit half covers
has not been compared.

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
