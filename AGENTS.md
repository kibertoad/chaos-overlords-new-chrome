# Repository agent instructions

## Git push destination

The authorized canonical repository is
`https://github.com/kibertoad/chaos-overlords-new-chrome.git`.
Before every push, inspect the repository's configured push destination with
`git remote get-url --push origin` (and `git remote -v` when additional context
is useful), and verify that it resolves to this canonical repository. Push
through the configured remote name and an explicit refspec, for example
`git push origin HEAD:main`.

Never rewrite, replace, or temporarily override a remote URL in order to push.
This prohibition includes `git remote set-url`, changing `remote.*.url` or
`remote.*.pushurl`, and command-scoped configuration such as
`git -c remote.origin.pushurl=...`. If the configured destination is missing or
does not match the repository the user authorized, stop and ask the user to
correct or approve the remote configuration instead of modifying it.

## Validation scope

Do not run the full test suite by default. It contains deliberately separated
long-running campaign coverage and takes too long for routine changes. Use the
default fast gate in `tools/Invoke-Validation.ps1`, or pass `-TestFilter` for a
smaller relevant scope. Run with `-IncludeLongRunningTests` or otherwise execute
the full suite only when the user explicitly requests it or when a specific
change to long-running coverage provides a documented exceptional reason.

On Windows, the PowerShell execution policy may reject repository scripts and
the `pnpm.ps1` command wrapper. Invoke the fast gate explicitly with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Invoke-Validation.ps1
```

Pass validation arguments after the script path when needed. Run pnpm-based
validators through `pnpm.cmd` (for example,
`pnpm.cmd --filter @chaos-overlords/contracts test:run`) so PowerShell does not
select the blocked `pnpm.ps1` wrapper.

## Documentation

The project follows the [methodology](https://dinorefurb.com/methodology/) and
version 1 of the
[documentation standard](https://dinorefurb.com/documentation-standard/)
published at dinorefurb.com. This section summarizes them; where they differ,
the published pages win.

### The spec

`spec/` documents the original game and nothing else, one entry per file named
after its ID: builds (`BLD-`), sources (`SRC-`), findings (`FND-`),
experiments (`EXP-`), formats (`FMT-`, binary ones with a Kaitai `.ksy`
definition next to them), rules (`RULE-`), bugs (`BUG-`) and screens (`SCR-`).
`spec/README.md` holds the scope and the area list, `spec/glossary.md` the terms
the pseudocode uses, and `docs/SPEC-ENTRY-TEMPLATES.md` a blank entry of each
kind. The spec never names a class, file or setting of the rebuild, and never
reproduces content: texts, images, sounds, or the names and statistics of
individual gangs, items and sites. Constants the code does arithmetic with are
written down in full.

Record new evidence as a new entry: a static reading of the executable or a
data file is a finding (`method: static`, with addresses written `0x0043B290`
for the only build, `BLD-GOG-EN-1.1`), a run of the original is a dynamic finding
or an experiment. A rule, format or screen cites them in `evidence` and takes
the status they support: `unknown`, `sourced` (outside sources only),
`supported` (one kind of evidence from the original), `established` (a static
reading and a run of the original agree), `disputed` or `superseded`. There is
no other confidence scale. Unidentified functions and globals keep neutral names
(`fn_00472775`, `g_004A5ED8`) until evidence shows what they do. IDs are never
reused or renumbered and areas are never removed or renamed; an entry that
turns out wrong becomes `superseded`. `docs/SPEC-ID-MAP.md` gives the spec ID
of every identifier the documentation used before the standard.

Decompiler output, disassembly listings and analysis databases never go into
the repository. Tool procedure is in `docs/GHIDRA.md`.

### The rebuild's ledgers

- `DEVIATIONS.md` lists every place the rebuild departs from the spec on
  purpose, as `DEV-AREA-NNN` entries with a Default of `off`, `on` or
  `mandatory`. A setting starts `off`, with the original's behaviour, unless the
  entry's Justification argues that the rebuild's behaviour is strictly better:
  then it starts `on`, and a player who wants the original switches it off. A
  deviation with no setting is `mandatory`, and its Justification also says why
  the original's behaviour is not worth a setting. The fix of an unintended bug
  that players do not rely on is `on` without one. A quirk that may be
  deliberate or that players rely on is never strictly better, so its deviation
  starts `off`. The validation suite runs with every setting switched off, and a
  test that reaches a mandatory deviation cites its ID and allows for it.
- `PARITY.md` has one row per rule, format and screen entry that is not
  superseded, with how much of it the rebuild does and which tests compare the
  rebuild with evidence from the original. Behaviour without a spec entry gets
  an `unknown` entry before any code. Manual play never counts as a test.
- `docs/DECISIONS.md` keeps dated product and scope decisions that are not
  departures from the original (network play, saves, bug reports).

Code comments and tests cite the spec IDs they implement or check. A
placeholder, such as a guessed formula, carries a `PLACEHOLDER: <spec ID>`
comment, and that row of `PARITY.md` cannot be `complete` while it does.

### Checks

`node tools/check-spec.mjs` runs the standard's checks over `spec/`,
`PARITY.md` and `DEVIATIONS.md`, checks that every spec and deviation ID cited
in the code resolves, and rewrites the generated indexes in `spec/index/`;
`--check` fails on a stale index instead of writing it. It compiles the Kaitai
definitions when `kaitai-struct-compiler` (or the path in `KSC`) is available;
the CI fast gate installs a pinned release, so there they always compile.
The script is written to move into the shared toolkit.

`docs/README.md` catalogs the other documents. Their tables of contents and the
decision index are generated blocks between `<!-- doc-index:begin ... -->` and
`<!-- doc-index:end -->` comments; after editing headings in a document that
carries one, run `node tools/update-doc-indexes.mjs` (`--check` reports stale
blocks and broken relative links without writing). The fast gate
(`tools/Invoke-Validation.ps1`) runs both scripts in `--check` mode whenever
`node` is on the path and requires them in CI, so a stale block, a broken link
or a spec problem fails validation after the tests have run.
`docs/ASSET-CATALOG.md` is generated by `Rechaos.Extractor --catalog` and is
not edited by hand.

## Fidelity

The spec records the original exactly, bugs included. The rebuild keeps the
rules, balance, content, AI and pacing, including asymmetries, rounding,
ordering, timing, overflow behaviour and quirks players built strategies
around. Crashes, corrupted saves, game speed tied to the CPU clock, and logic
that plainly does not do what it was written to do may be fixed. An interface
change may add information or remove friction, and may not change what the
player can do or what the rules produce. Screens match the original pixel for
pixel except where a documented interface change draws something new. When a
bug cannot be told from a design decision, the original behaviour stays and any
fix becomes a setting.

## Multiplayer protocol version

Keep `MULTIPLAYER_PROTOCOL_VERSION` in
`multiplayer/packages/contracts/src/protocol.ts` and `MultiplayerProtocolVersion.Current` in
`src/Rechaos.Multiplayer/Protocol/MultiplayerProtocolVersion.cs` equal. Whenever a change can affect
communication between the game client and coordination server—including request or response
schemas, routes, authentication, event streams, serialization, or protocol behavior—increment both
versions in the same change. Never update only one side.

The protocol version decides one thing only: whether a client and a server can talk to each other.
It is settled by the handshake before any match data moves, and it is never the reason a stored
match is refused. Whether a match can still be played is the session version below.

## Multiplayer session version

Keep `MULTIPLAYER_SESSION_VERSION` in
`multiplayer/packages/contracts/src/protocol.ts` and `MultiplayerSessionVersion.Current` in
`src/Rechaos.Multiplayer/Protocol/MultiplayerSessionVersion.cs` equal, and increment both in the
same change or neither. It describes the session as it is stored—the match row, its turns, its
orders and its snapshots—and every match carries the version it was created under for the whole of
its life. A client resumes a match only when the stored session version is the one it plays, so a
bump retires every match in progress: players lose the seats they are holding, and the previous
sessions browser and the public match list leave those matches out instead of offering them.

Bump the session version when a build could no longer correctly carry on a match an older build
started:

- the deterministic rules or the resolution of a turn change, so replaying the same sealed orders
  reaches a different state;
- the order document's schema or the meaning of an op changes;
- the settings a city is generated from, the seeding, or the seat assignment change;
- the state hash is computed differently, or the native save format stops round-tripping;
- a stored field's meaning changes, rather than a new one being added.

Leave it alone for everything else, including changes that do move the protocol version: a new
endpoint or field, a renamed or reshaped request or response, authentication, the event stream,
serialization, retention or any other server-side behavior that leaves the match a client resumes
identical. A session version that shadowed the protocol version would throw away live matches for
wire changes that never touched them.

Two things follow a bump. Both mirrors move in the same change, and the protocol rule above still
applies on its own terms, so a change that alters the wire as well moves both numbers. No migration
is written for the stored sessions: the server holds them as opaque history and only a client can
read one, so a match from an older session version is refused rather than reinterpreted.

## State fingerprint format

`MatchStateHasher.FormatVersion` in `src/Rechaos.Core/GameModel/Determinism.cs` identifies the
encoding a state fingerprint is computed from. Increment it whenever that encoding changes, so no
two encodings share a fingerprint space.

Nothing on disk or on the wire records which encoding produced a stored fingerprint. A fingerprint
from an older encoding is well-formed and simply fails to compare equal, and a mismatch is read as
damage or as divergence, never as an older file. The version gate in front of a stored fingerprint
is therefore the only thing that can refuse one, so every gate moves in the same change:

- `NativeSaveSerializer.CurrentFormatVersion`, for the fingerprint and the phase-hash history a
  save carries. Left behind, a save is drawn as playable by the save browser, fails verification as
  a plain `InvalidDataException` that `IncompatibleSave` does not recognise, and is therefore taken
  for damage: the backup generation is judged not worth keeping and the next save overwrites it.
- `MatchReplaySerializer.CurrentFormatVersion`, for the step fingerprints a journal is verified
  against. Left behind, a journal passes the gate and is reported as a divergence on its first
  step, and a resume drops it silently.
- `MULTIPLAYER_SESSION_VERSION` and `MultiplayerSessionVersion.Current`, under the rule above — a
  stored match whose turns were sealed under the old encoding cannot be carried on.

`StateFingerprintVersionCouplingTests` pins all four numbers together and fails when one moves
alone. A failure there is the question, not the answer: decide which of the versions the change
reaches, then pin the new set.

## Post-commit orphan-process audit

After every commit in this repository, inspect running processes for orphaned
work created by this repository's tasks. Check at least PowerShell
(`powershell` and `pwsh`), Ghidra/Java, .NET (`dotnet` and `testhost`), and any
other process families that the agent launched while building, testing,
validating, or analyzing this repository.

Reusable MSBuild nodes (`dotnet` running `MSBuild.dll` with `/nodeReuse:true`)
are expected background workers, not orphans. Do not stop or log them merely
because their spawning build process exited, their start time matches a
repository validation, or they remain idle after a build. They are exempt from
cleanup unless there is separate evidence that the process is malfunctioning
and must be stopped to complete this repository's work. Do not disable MSBuild
node reuse in routine build or test commands; keeping these workers available
makes subsequent builds faster.

Multiple conversion agents normally work in parallel, but they work on other
games. A process may be treated as belonging to this work when its command line,
parent, task/session, or source paths show that it targets this repository or
the reference installation/assets at `C:\GOG Games\Chaos Overlords`. Do not
terminate a process merely because its executable name matches. Preserve
processes for other games, unrelated user/IDE/system processes, and validation
that is intentionally still running. If repository/source ownership is
uncertain, leave the process running.

Stop confirmed orphaned repository processes. Whenever any process is stopped,
append an entry to `orphanCleanupLog.md` containing:

- the local timestamp including UTC offset;
- each stopped PID and process name;
- its start time or task/session association when known;
- why it was identified as orphaned;
- any related process deliberately left running and why.

If the audit finds nothing to stop, no log entry is required.
