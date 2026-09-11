# Development handover

Status: ready to resume
Last updated: 2026-09-11

## Repository state

- Work continues on `codex/full-reimplementation` at
  `https://github.com/kibertoad/chaos-overlords-new-chrome.git`.
- The latest pushed functional checkpoint is the tip of that branch.
- The canonical local gate is `./tools/Invoke-Validation.ps1`. The latest
  isolated Release build passed all 1,046 tests with no warnings.
- Validation deliberately stops only a development `Rechaos.Game` executable
  located inside this checkout, serializes concurrent validation attempts, and
  caps MSBuild at two workers. It retains incremental outputs and compiler/build
  server reuse. Use `-ShutdownBuildServersAfterRun` only to clear stale servers;
  it can also make the next IDE build cold.
- Native saves are format v16, replays are v17, canonical hashes are v19, asset
  manifests are v4, extracted help is v1, and client preferences are v5. Save
  and replay compatibility may intentionally break before 1.0.0; retain the
  migration/versioning machinery for post-1.0 compatibility.

## Latest playable work

- The title and in-game Help commands open a cross-platform viewer backed by
  the locally extracted original WinHelp content. Mouse-wheel scrolling follows
  the topic-list/content pane under the pointer. The Attack topic carries the
  corrected Force-inclusive simultaneous-combat explanation.
- Music and sound effects have independent recovered 0-10 controls and persisted
  defaults. Title/setup push buttons, setup selection/rejection, panel
  confirmation, equipped and unarmed gang attacks, retaliation, detected police
  attacks, idle planning confirmation, pending Last Turn Events, and planning-countdown
  warnings use mapped sounds. General slot 9 is loaded by the original but has
  no call through its gated effect wrapper. Detailed Combat cues start with
  their corresponding animation clips rather than at resolution time.
- Setup now offers the original None, 30 Seconds, 2 Minutes, and 5 Minutes
  planning limits. Human planning displays the recovered 60-by-3 bar, continues
  through planning panels, uses the original percent-first width quantization,
  checks warning slots 7/8 every sixth fixed update, and finishes through the
  ordinary replay-recorded operation when time expires. Computer turns and Core
  deterministic state never use wall-clock time.
- Every recovered AI strategy family has a live handler and the earlier cleanup
  pass separates dispatch, immutable planning facts, recovered shared operations,
  and explicitly provisional fallback scoring. The next AI gate is evidence,
  not another structural rewrite: fixed original-runtime traces plus multi-seed
  tournament coverage. Six-computer deterministic/replay fixtures now exercise
  all objectives for a 208-turn horizon. Big Man completes at the guarded seed;
  Kill 'Em All, Big 40, Eliminate, Siege, and Armageddon remain valid but
  unfinished, so their completion policy is still an explicit M6 gap.
- Options now uses the recovered defaults: Current gang statistics, Detailed
  Combat on, Slide Panels on, and Warn If Idle Gangs on, and persists those
  choices alongside audio and the planning timer. Version-4 preferences migrate
  forward using the recovered defaults for the three choices that format lacked.
  Enabled panel entrances use the recovered horizontal 344-pixel primary travel
  and 250 ms benchmark target. The legacy 16-bit color choice is explicitly
  always enabled by the modern renderer.
- Combat Summary now replays the selected result through the detailed combat
  panel in either presentation mode. Escape or the panel Cancel control clears
  the queue immediately; cancellation and large-elapsed stress tests guard the
  no-hang presentation boundary. A twin-resolution fixture also drains Detailed
  playback and proves its rolls, Force, phase hash, and full authoritative hash
  remain identical to Simple presentation.
- Snapshot and replay stores read back each flushed temporary generation before
  promotion, retain the last valid primary as a backup, and do not poison a good
  backup when replacing a corrupt primary. F10 transparently verifies the
  replay backup when the primary is missing or invalid.
- Fresh Siege matches now designate all six assigned starting HQ sectors as
  important, matching the manual's setup rule, and the city renders two gray
  pylons in each objective tile. This closes the unwinnable generated-Siege gap;
  exact original pylon art remains a visual-capture task.

## Reference environment

- Legal GOG installation: `C:\GOG Games\Chaos Overlords`.
- Supported executable: 664,576 bytes, SHA-256
  `a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89`.
- Check the pinned Ghidra installation at
  `C:\Users\kiber\AppData\Local\Programs\Ghidra\ghidra_12.1.3_PUBLIC`
  before searching for or installing tooling. Follow [GHIDRA.md](GHIDRA.md)
  and keep executables, projects, dumps, and decompiler output outside Git.

## Recommended next evidence batches

1. Capture the original planning countdown to settle wall-clock warning cadence,
   modal behavior, and deactivation timing; adjust presentation only
   where the capture contradicts the current bounded implementation.
2. Capture the original panel/combat cadence, identify the adjacent-buffer
   320-pixel panel form and close behavior, then adjust presentation where
   the reference contradicts it.
3. Investigate and explain the reported GOG/1.1 Detailed Combat freeze, then
   compare the bounded recreation cadence with a controlled original capture.
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
