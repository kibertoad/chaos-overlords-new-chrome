# Local automated checks

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

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
