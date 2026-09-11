# Development handover

Status: ready to resume
Last updated: 2026-09-11

## Repository state

- Work continues on `codex/full-reimplementation` at
  `https://github.com/kibertoad/chaos-overlords-new-chrome.git`.
- The latest functional checkpoints are committed on that branch; publish the
  local commits when repository push authorization is available.
- The canonical local gate is `./tools/Invoke-Validation.ps1`. The latest
  isolated Release build passed all 1,139 tests with no warnings.
- Validation deliberately stops only a development `Rechaos.Game` executable
  located inside this checkout, serializes concurrent validation attempts, and
  caps MSBuild at two workers. It retains incremental outputs and compiler/build
  server reuse. Use `-ShutdownBuildServersAfterRun` only to clear stale servers;
  it can also make the next IDE build cold.
- Native saves are format v19, replays are v20, canonical hashes are v22, asset
  manifests are v4, extracted help is v1, and client preferences are v6. Save
  and replay compatibility may intentionally break before 1.0.0; retain the
  migration/versioning machinery for post-1.0 compatibility.

## Latest playable work

- Handoff now auto-presents combat from the immediately completed turn. When
  both report types exist, Last Turn Events opens first and then chains into
  Combat Results; Detailed animation capture waits until private handoff/event
  panels have closed. Its event cursor is per player, so one hot-seat viewer
  cannot consume another's Detailed playback; loading initializes every cursor
  past historical events. Manual Combat Summary also refuses an empty result set
  and no longer accumulates historical combats from every prior turn.
- Last Turn Events now tracks which report pages were actually visited. Closing
  before viewing every page preserves the report queue and blinks the Events
  control; viewing all pages clears it through replay-recorded dismissals. The
  Events control also refuses to open an empty panel.
- Single-player objective games now end at the Player Elimination boundary when
  their sole human Overlord is eliminated, recording the distinct
  `PlayerEliminated` outcome. The elimination splash returns to the title as the
  Help specifies; hot-seat games continue after one human is eliminated.
- A completed single-player match now shows the original `PX00202` victory or
  `PX00203` elimination splash, including the configured human Overlord portrait,
  before advancing to the awards/statistics screen when victorious. That screen now uses the
  six original portrait rows, `PX00201` award icons, and an Awards/Stats toggle
  exposing Cash Earned, Cash Spent, Damage Inflicted, Casualties, and Overthrows.
  Hot-seat matches still go directly to shared standings until the original
  private sequencing is captured.
- Search now uses the original `PX05024` Search: Sites panel instead of the
  earlier incorrect detected-gang list. Its two-column aperture contains all 22
  site types with ALL/NONE and individual toggles; OK applies a presentation-only
  cyan outline to matching city sectors. The panel identity and geometry are
  directly visible in the asset, while the post-confirmation outline remains
  provisional pending an original runtime capture.
- Player Ranking now uses `PX05011` and positions every active Overlord portrait
  on its original color rail by canonical timed score or objective progress.
  Competition ties share a height and eliminated players disappear.
- Gangs/Sector now uses the original `PX05009` stat browser, opens only the
  active player's gangs in the selected sector, rejects an empty sector, and
  cycles within that stable local roster. Direct live Gang Information uses
  `PX05000` and renders three equipped-item cells; hire offer inspection uses
  the equipment-free `PX05022` definition template.
- Finance now uses the original paired `PX05008` City Financial and `PX05019`
  Sector Financial panels selected by the split main-console control. The
  non-mutating projection covers active and pending upkeep, recruit cost and
  projected gang count, queued equipment and bribe cash, taxes, influenced-site
  income, estimated Chaos, and the net adjustment with original red/green signs.
- Ending planning with an unassigned active gang now displays the original
  `PX05020` System Warning panel. Its baked Cancel control returns to planning;
  OK confirms the same authoritative end-turn flow as before.
- The main console's original Game Info button now opens the `PX05021`
  Scenario Information panel over either City or Sector. It lists the scenario,
  AI mentality, planning limit, and all six color-coded players with the
  manual-defined Human/AI intelligence labels. As documented by the original
  help, it auto-opens when a local multiplayer match starts and whenever a live
  saved game is loaded.
- Local setup now begins with one human, Add/Remove changes the local-human
  count, and Begin lets the recovered factory fill all omitted slots as
  Computers. Clicking a visible player's name edits the original bounded
  10-character field, enabling ordinary custom names and the recovered
  exact-name modifiers; portrait 15 is no longer selectable as a human face.
  Dragging a human face to an empty color moves that identity into the sparse
  slot; dropping onto another human exchanges their colors. Begin then fills
  missing color slots in ascending order before AI and city RNG consumption.
- The main console now routes its split Comlink controls to the original
  `PX05017` incoming-message viewer and `PX05018` sender. Human players can page
  the newest 16 messages, see unread-state blinking, select multiple human
  recipients, enter the recovered four 40-character rows, and send through the
  authoritative replay-recorded operation. Viewing clears unread state through
  that same authoritative path.
- The title and in-game Help commands open a cross-platform viewer backed by
  the locally extracted original WinHelp content. Navigation follows the 59
  player-facing entries in the original contents order and omits 21 unlisted
  internal fragments; documents without a contents table safely fall back to
  all decoded topics. Mouse-wheel scrolling follows the topic-list/content pane
  under the pointer. Game Info, Give, Sell, Comlink, Events, Search, and Combat
  now open their specific original topics. The Attack topic carries the
  corrected Force-inclusive simultaneous-combat explanation.
- Music and sound effects have independent recovered 0-10 controls and persisted
  defaults. Full local-setup push buttons, setup selection/rejection, panel
  confirmation, equipped and unarmed gang attacks, retaliation, detected police
  attacks, idle planning confirmation, unread Comlink handoff entry, and
  planning-countdown warnings use mapped sounds. General slot 9 is loaded by the original but has
  no call through its gated effect wrapper. Detailed Combat cues start with
  their corresponding animation clips rather than at resolution time.
- The four original Add/Remove/Begin/Cancel setup hit rectangles now defer their
  action until release inside the same pressed control, cancel release outside,
  and show their exact `PX00140` held-inside tiles.
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
  all objectives through live-equivalent 40-turn campaigns with offer refills,
  resolved hires, territorial expansion, and replay verification. Big Man
  completes by turn 60 at the guarded seed; Kill 'Em All, Big 40, Eliminate,
  Siege, and Armageddon still need evidence-led completion policy, so that
  remains an explicit M6 gap. Eliminated planning slots are skipped through
  replay-recorded transitions, and negative effective Stealth is safely bounded
  to 100% police detection.
- Options now uses the recovered defaults: Current gang statistics, Detailed
  Combat on, Slide Panels on, and Warn If Idle Gangs on, and persists those
  choices alongside audio, the planning timer, and a recreation-native
  windowed/borderless-fullscreen mode. F11 switches display mode from any screen
  and the choice survives relaunch; version-4 and version-5 preferences migrate
  forward with a safe windowed default.
  Enabled panel entrances use the recovered horizontal 344-pixel primary travel
  and 250 ms benchmark target. The legacy 16-bit color choice is explicitly
  always enabled by the modern renderer.
- A press edge on the right mouse button now cancels the active transient edit,
  pressed setup control, drag, warning, Detailed Combat playback, or nested
  panel through that workflow's existing close/back path. This preserves dynamic
  return screens and prevents held-button repeats; idle right-clicks on title,
  city, handoff, and endgame do nothing.
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
- Sell now uses the original `PX05013` Equipment to Sell panel. Any combination
  of the acting gang's three equipped slots can be highlighted and confirmed in
  one command. The binary clears every selected slot but overwrites one payout
  local in weapon/armor/miscellaneous order, so compatibility resolution credits
  only the highest selected slot's half raw price, rounded down, without Factory.
- Factory integration now has a combined acquisition/replacement fixture:
  Influence completed during Instant makes the local Factory available to a
  same-turn Transaction Equip, which replaces the old slot item and charges the
  recovered `Cost - trunc(Cost / 3)` price. The odd-priced $11 Katana fixture
  therefore costs $8 rather than the previous provisional $7. Manual-backed
  match validation rejects influenced sites
  in neutral sectors or sites influenced by anyone other than the sector owner;
  binary addresses and operation order are recorded in `ORIGINAL-INTERNALS.md`.
- Instant resolution now snapshots acting gangs' effective statistics before
  applying any command. Same-phase Influence can still acquire a site, but its
  modifiers cannot leak into concurrent Heal, Research, or grouped Influence
  rolls; the Science Center regression fixture guards this simultaneous boundary.
- Control conflicts now use one phase-opening owner and defense snapshot for all
  player groups in a sector. The binary-recovered candidate list chooses equal
  positive leaders randomly in ascending player-slot order; at zero margin it
  places neutral/no-capture before every tied player. The recorded one-based
  roll preserves that ordering, a controlled sector can be overthrown only once,
  and an execution-time Crackdown rejects every competing group.
- A complete owner-field scan of the original whole-turn resolver found writes
  only for the third-Crackdown neutralization and a successful Control winner.
  Moving or terminating the last friendly gang therefore leaves sector ownership
  intact; dedicated fixtures now prevent accidental auto-abandonment.
- Control and Chaos regression fixtures now make density-derived sector Income
  differ from summed site Cash, guarding the recovered distinction. Chaos adds
  sector Income separately to every participating gang's pool before grouping.
- Give now uses the original `PX05015` Equipment to Give panel before its
  compatible same-sector recipient list. One command can carry any combination
  of the source gang's exact three equipped items. Transactions now scan fixed
  player and roster slots, reserve outgoing items, and apply incoming gifts only
  after recipient transactions, allowing swaps and overwriting same-turn buys
  exactly as the resolver does. Native saves are v19, replays are v20 and canonical hashes
  are v22; the immediately previous formats
  remain readable through their preserved fingerprint projections.
- Move now uses the original `PX05006` Movement panel. Its destination aperture
  is an exact 3-by-3 composition of the city map's native 54-by-52 ownership
  tiles around the acting gang, with mouse and directional-key selection limited
  to the authoritative adjacent-sector command options.
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
