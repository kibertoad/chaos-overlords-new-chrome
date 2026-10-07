# Recreation validation procedure

Status: maintained canonical procedure

<!-- doc-index:begin toc depth=2 -->
- [Validation layers](#validation-layers)
- [Procedures](#procedures)
- [Current canonical identities](#current-canonical-identities)
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

## Procedures

Each part of the procedure has its own document under `docs/validation/`:

| Document | Covers |
|---|---|
| [local-checks.md](validation/local-checks.md) | The fast gate `tools/Invoke-Validation.ps1`, its modes and discovery counts, the long-running tier, the `ai-tournament` runner, build servers, the repository policy and the diagnostics, persistence and online-session tests |
| [ci-and-packaging.md](validation/ci-and-packaging.md) | The continuous-integration and packaging workflows, hosted runs, installers and signing, and the pinned Actions |
| [asset-and-state-tools.md](validation/asset-and-state-tools.md) | Verifying an original installation and an asset pack, the asset catalog, the RGB555/RGB565 comparison, `state-diff` and the reference fixture contract |
| [test-harnesses.md](validation/test-harnesses.md) | Simulated human seats and the headless game |
| [experiments.md](validation/experiments.md) | Experiments on the original and the probe that records them |
| [static-research-and-spec-checks.md](validation/static-research-and-spec-checks.md) | Static binary research and the spec checks |
| [tests-against-the-original.md](validation/tests-against-the-original.md) | Which tests a parity row lists, and the deviation settings a test runs with |
| [screen-captures.md](validation/screen-captures.md) | Taking a capture of the original's screen |
| [screen-comparison.md](validation/screen-comparison.md) | Comparing the rebuild's reference frame with a capture, and what the reference frame shows |
| [screen-capture-coverage.md](validation/screen-capture-coverage.md) | Which captures cover each screen entry, and the states no capture shows |
| [native-checks.md](validation/native-checks.md) | The native audio backend, the pattern fill reference and the original pattern resources |
| [diagnostic-comparisons.md](validation/diagnostic-comparisons.md) | One-off pixel comparisons made with external diagnostic builds |

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
