# Development handover

Status: ready to resume
Last updated: 2026-09-11

## Repository state

- Work continues on `main` at
  `https://github.com/kibertoad/chaos-overlords-new-chrome.git`.
- The last pushed functional checkpoints are `86b2f08` (planning timer),
  `20a7500` (bounded serialized validation), and `b74bc01` (renamed repository
  links). The handover/documentation commit follows those checkpoints.
- The canonical local gate is `./tools/Invoke-Validation.ps1`. The latest run
  built Release with no warnings and passed all 1,018 tests.
- Validation deliberately stops only a development `Rechaos.Game` executable
  located inside this checkout, serializes concurrent validation attempts, and
  caps MSBuild at two workers. It retains incremental outputs and compiler/build
  server reuse. Use `-ShutdownBuildServersAfterRun` only to clear stale servers;
  it can also make the next IDE build cold.
- Native saves are format v16, replays are v17, canonical hashes are v19, asset
  manifests are v4, extracted help is v1, and client preferences are v4. Save
  and replay compatibility may intentionally break before 1.0.0; retain the
  migration/versioning machinery for post-1.0 compatibility.

## Latest playable work

- The title and in-game Help commands open a cross-platform viewer backed by
  the locally extracted original WinHelp content. Mouse-wheel scrolling follows
  the topic-list/content pane under the pointer. The Attack topic carries the
  corrected Force-inclusive simultaneous-combat explanation.
- Music and sound effects have independent recovered 0-10 controls and persisted
  defaults. Setup selection/rejection, panel confirmation, weapon attacks, idle
  planning confirmation, and planning-countdown warnings use mapped sounds.
- Setup now offers the original None, 30 Seconds, 2 Minutes, and 5 Minutes
  planning limits. Human planning displays the recovered 60-by-3 bar, continues
  through planning panels, uses warning slots 7/8, and finishes through the
  ordinary replay-recorded operation when time expires. Computer turns and Core
  deterministic state never use wall-clock time.
- Every recovered AI strategy family has a live handler and the earlier cleanup
  pass separates dispatch, immutable planning facts, recovered shared operations,
  and explicitly provisional fallback scoring. The next AI gate is evidence,
  not another structural rewrite: fixed original-runtime traces plus larger-player
  and objective-completion stress coverage.

## Reference environment

- Legal GOG installation: `C:\GOG Games\Chaos Overlords`.
- Supported executable: 664,576 bytes, SHA-256
  `a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`.
- Check the pinned Ghidra installation at
  `C:\Users\kiber\AppData\Local\Programs\Ghidra\ghidra_12.1.3_PUBLIC`
  before searching for or installing tooling. Follow [GHIDRA.md](GHIDRA.md)
  and keep executables, projects, dumps, and decompiler output outside Git.

## Recommended next evidence batches

1. Capture the original planning countdown to settle bar rounding, warning
   cadence, modal behavior, and deactivation timing; adjust presentation only
   where the capture contradicts the current bounded implementation.
2. Recover and implement the remaining local Options behaviors: base statistics,
   detailed/simple combat presentation, sliding panels, and color-depth handling
   or an explicit modern classification where the legacy choice is meaningless.
3. Investigate the reported GOG/1.1 Detailed Combat freeze, preserve identical
   combat mechanics between presentation modes, and add a no-hang stress gate.
4. Recover WinHelp links, inline formatting, and context IDs, then expand exact
   contextual Help entry points.
5. Complete remaining sound triggers, native audio/music validation, and the
   Smacker video playback/transcode decision.

For authoritative scope and parity status, continue with
[IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md) and
[PARITY-MATRIX.md](PARITY-MATRIX.md). Record new static findings in
[ORIGINAL-INTERNALS.md](ORIGINAL-INTERNALS.md), intended mechanics in
[GAME-RULES.md](GAME-RULES.md), and player-visible AI behavior in
[AI-SPEC.md](AI-SPEC.md).
