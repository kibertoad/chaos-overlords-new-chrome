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
- [Failure triage](#failure-triage)
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
captured test output and completed-case names. The long-only mode requires all
53 cases to be discovered. `-TestFilter`, `-IncludeLongRunningTests`, and
`-LongRunningTestsOnly` are mutually exclusive so the selected scope remains
unambiguous. Every invocation retains the 30-minute global test safety timeout.

The manually dispatched CI workflow runs the fast tier on Windows x64, Linux
x64, macOS arm64, and macOS x64. Its `fast-and-long-running` option adds the
observable long category once on Linux. A separate workflow runs the observable
long category on Linux every day at 03:17 UTC, but skips scheduled execution when
the default branch has no commit from the preceding 24 hours. Manual dispatches
always run it. The release workflow runs the fast tier only, leaving the repeated
statistical campaign matrix off its critical path.

For larger statistical samples, the presentation-free runner avoids xUnit and
lets replay verification be sampled rather than paid for on every match:

```powershell
dotnet run --project src/Rechaos.Tools -c Release --no-build -- ai-tournament `
  --matches 60 --turns 40 --workers 2 --policy original `
  --scenarios objectives --replay-every 10 --trace
```

Its periodic heartbeat remains visible without `--trace`; the trace adds each
live case's scenario, seed, turn, boundary count, event count, and elapsed time.
The final JSON includes deterministic hashes and territory, defended-territory,
gang, combat, replay, and timing metrics. Run the same matrix with `--policy
advanced` for a paired comparison.

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
explicit clean-room or synthetic fixture roots, and unreviewed files larger
than 1 MiB. The Windows publisher invokes the same check before deleting or
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
`PARITY.md` and `DEVIATIONS.md`, and writes the indexes in `spec/index/`. The
fast gate runs it with `--check`. It compiles the Kaitai definitions when
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
