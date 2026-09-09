# Recreation validation procedure

Status: initial executable procedure
Last updated: 2026-09-07

## Validation layers

Accuracy is established separately at four layers:

1. **Source identity** - original input files match known cryptographic hashes.
2. **Decode fidelity** - bytes are consumed at exact offsets into exact values.
3. **Behavioral parity** - controlled inputs produce matching state transitions.
4. **Presentation parity** - the same state produces equivalent screens/media.

A pass at one layer does not imply a pass at the next.

## Local automated checks

```powershell
./tools/Verify-Repository.ps1
dotnet restore Rechaos.slnx
dotnet build Rechaos.slnx --no-restore
dotnet test --project tests/Rechaos.Tests/Rechaos.Tests.csproj --no-build `
  --no-progress --minimum-expected-tests 332
```

The manually dispatched continuous-integration workflow runs this verification
on Windows x64, Linux x64, macOS arm64, and macOS x64. It also publishes with
`IncludeOriginalAssets=false`, rejects any resulting `Assets` directory, and
runs the published `--smoke-test` entry point without an original asset pack.
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

The first complete hosted run of this matrix and installer path was GitHub
Actions run `34400362789` on 2026-09-09; every platform and packaging job
passed. Hosted runs remain the authority for runner-specific compatibility.

Linux and macOS installer builders run only on matching native hosted runners.
The Linux gate opens the generated `.deb` and smoke-runs its installed-layout
executable. Each macOS gate validates the generated plist, smoke-runs the app
bundle executable, builds the `.pkg`, expands it again, and confirms the game
payload. Windows additionally exercises silent install and uninstall. Unsigned
packages are development artifacts until the signing/notarization release gate
is implemented.

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
log. The current installed pack contains 685 manifest entries representing 471
original source resources; 214 entries are decoded PX08 derivatives retained
alongside their original inputs.

## Original-binary oracle protocol

Run the original only offline in a controlled Windows environment. Record:

- executable/data hashes and OS compatibility settings;
- experiment ID and the rule/finding under test;
- complete new-game options and visible initial state;
- pre-action save and screenshot;
- exactly one intentional input variable;
- all visible results, notification order, animations and sounds;
- post-action save and screenshot;
- repeat count and observed result distribution;
- analyst conclusion, contradictions and confidence change.

Never infer a formula from one stochastic sample. Hold every possible variable
constant, repeat, and use save-state differences to identify the fields that
changed. For ordering/RNG research, start repeated branches from the identical
pre-action save.

## Static binary research protocol

- Fingerprint the exact executable before analysis.
- Work from facts: constants, data references, branches, state offsets and call
  relationships. Do not copy decompiled implementation code.
- Assign neutral names until behavior confirms semantics.
- Link each recovered fact to executable hash, address/range, tool version and a
  reproducible navigation description.
- Correlate static findings with controlled state/save experiments.
- Record unresolved branches and alternate interpretations.
- Keep raw analysis databases and executable material outside the repository.

Static analysis should prioritize the phase dispatcher, RNG, save/load, city
generation, action resolver, scoring/victory, AI, and resource lookup. Legacy
network message boundaries are deliberately outside the recreation scope.

## Fixture classes

- **Format fixture:** synthetic non-copyrighted bytes testing parser boundaries.
- **Definition fixture:** checked-in mechanical values with pinned provenance.
- **State fixture:** sanitized structured pre/post state from an experiment.
- **Command fixture:** initial state, input commands, expected events and hashes.
- **Distribution fixture:** repeated stochastic result counts and tolerance.
- **Visual fixture:** extracted locally and never committed; comparison metadata
  and masks may be committed.
- **Save fixture:** only when redistribution status is clear; otherwise generated
  locally from documented steps.

## Required parity record fields

Every `PARITY-MATRIX.md` row must identify original behavior, implementation,
evidence/finding ID, confidence, automated test, and status. A behavior cannot be
`Parity verified` when its evidence is manual-only or its implementation contains
placeholder formulas.

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
