# Development handover

Status: active at a validated local checkpoint
Last updated: 2026-09-11

## Repository state

- The canonical repository is
  `https://github.com/kibertoad/chaos-overlords-new-chrome.git`. All functional
  checkpoints through native WinHelp context recovery are published on `main`;
  the local `codex/full-reimplementation` branch additionally contains the
  styled-text and internal-hotspot batch and remains the development branch.
- The canonical local gate is `./tools/Invoke-Validation.ps1`. The latest
  isolated Release build passed all 1,271 tests with no warnings.
- Validation deliberately stops only a development `Rechaos.Game` executable
  located inside this checkout, serializes concurrent validation attempts, and
  caps MSBuild at two workers. It retains incremental outputs and compiler/build
  server reuse. Use `-ShutdownBuildServersAfterRun` only to clear stale servers;
  it can also make the next IDE build cold.
- Native saves are format v19, replays are v21, canonical hashes are v22, asset
  manifests are v6, extracted help is v3, and client preferences are v6. Save
  and replay compatibility may intentionally break before 1.0.0; retain the
  migration/versioning machinery for post-1.0 compatibility.

## Latest playable work

- Setup selection outlines now use the inset `PX00143` button faces instead of
  the broader hit rectangles that overlap section labels. The city/sector top
  bar uses the original portrait aperture and plays the twelve-frame
  `PX00129` active-player marker beside the current Overlord. Recreation-only
  navigation, drag, cancellation and success hints are suppressed while
  rejection reasons and genuine failures remain visible.
- Completed-Research artwork now occupies the exact 48-by-48 black monitor
  interior in `PX06005` instead of extending above and left of it. City and
  Sector Financial panels now fill their full 64-by-64 portrait aperture.
- Item Information now fills the exact 48-by-48 monitor aperture with the
  selected item's 15-frame `PX04xxx` rotation, using the compact inventory icon
  only as a centered fallback. Shared gang-command portraits, Attack equipment
  and opponent cells, and Site Information art now use their measured template
  apertures instead of the earlier one- or two-pixel offsets.
- Gang Information now slides independently over a stationary City or Sector
  backdrop. Opening the command picker no longer applies a panel entrance, and
  closing nested details back to Sector no longer replays the Sector entrance.
  Disabling Slide Panels globally cancels any in-flight entrance, so Sector and
  every detail panel appear immediately and cannot resume a stale transition.
  The provisional `PLAN YOUR TURN` and `SECTOR n DETAIL` status hints were
  removed.
- Scenario Information fields now follow the exact placeholder baselines and
  nine-pixel player-row pitch embedded in `PX05021`, preventing the settings
  and six-player roster from colliding with its labels and lower divider.
- Every objective predicate now requires an active Overlord. An eliminated
  player can no longer win from retained Big Man points or other stale objective
  projections; exact-threshold, nearest-incomplete, eliminated-player, and
  simultaneous-active-winner fixtures cover all six objective scenarios.
- Every Options entry now exposes a bounded pointer-hover explanation. The
  legacy Thousands of Colors row explicitly explains that the modern renderer
  is always above 16-bit and that no changeable retro-color mode is currently
  planned; Slide Panels explains the global immediate-display behavior when off.
- Recurring commands are now limited in both the picker and authoritative
  validation to Bribe, Chaos, Control, Heal, Hide, Influence, Research, and
  Snitch. Attack, Equip, Give, Move, Sell, and Terminate remain one-off actions.
- City Financial, Comlink View, and Gangs in Sector dynamic fields now follow
  their original template baselines and apertures. Research selection paints an
  active OK state for a valid item and shows accumulated/required progress.
  Completed-research reports name the resolved item and play its dedicated
  15-frame `PX04xxx` rotation in the monitor.
- The `PX05016` Gangs for Hire comparison now opens at the shared management
  panel destination instead of screen origin. Its three portraits, right-aligned
  values, and irregular sixteen-row baselines follow the template pixels.
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
  Help specifies; hot-seat games continue after one human is eliminated only
  while at least two Overlords remain active.
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
  on its original color rail by the recovered scenario score table.
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
  There is no separate Human/AI toggle: Add/Remove determines how many local
  humans are configured, while every omitted color slot becomes a Computer at
  Begin. The top-strip faces and two-by-three editable cards now use the measured
  `PX00143` apertures plus the `PX00140` arrow/name construction offsets, without
  the recreation-only player-count label or portrait border.
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
  under the pointer. Asset-pack format 6 preserves all 80 native `|CONTEXT`
  hash/target pairs, verifies every one of the 59 `CHAOS.CNT` context names, and
  records that this file's `|CTXOMAP` contains no numeric IDs. Contextual F1
  routing now uses those exact symbols rather than ambiguous topic-title
  matching; title matching remains only as a bounded fallback. Extracted-help
  format 3 also preserves 779 normalized authored runs from nine legacy font
  descriptors and all 93 internal hotspots: 67 topic jumps navigate in place
  and 26 popup links expose the unlisted definition fragments modally. Bold,
  italic, underline, double-underline, strikeout, small-caps, and source size
  metadata are retained; the pixel viewer renders emphasis, link underlines,
  and popup modality without executing macro or external-file commands. The Attack topic
  carries the corrected Force-inclusive simultaneous-combat explanation.
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
  modifiers cannot leak into concurrent Heal, Research, or per-gang Influence
  rolls; the Science Center regression fixture guards this simultaneous boundary.
  The binary's fixed player/roster-slot scan is reproduced, so friendly gangs
  roll and apply Influence separately and a later gang consumes no RNG after an
  earlier roster slot completes the site.
- Bribe now follows the executable rather than the contradictory printed rule:
  it requires and spends $3, records $3 spent, and adds 3 directly to effective
  tolerance without the recreation's former 40-point base clamp. Finance and AI
  affordability use the same shipped cost; the $5/cap helper is manual-only.
- Snitch likewise follows the resolver: it subtracts 3 even in debt, then one
  global post-Instant pass floors every sector at tolerance 1. This replaces the
  provisional per-command zero floor and negative-tolerance automatic Crackdown.
- Research now honors the resolver's per-item completion guard inside the fixed
  player/roster scan: once an earlier gang completes an item, later queued gangs
  emit no roll and consume no RNG for it in that Instant phase.
- End-turn evaluation now applies the binary's scenario-independent survivor
  rule: exactly one active Overlord ends any timed or objective match early.
- Player Ranking and persisted endgame standings now share the executable's
  all-scenario score table. Objective scores no longer substitute victory
  progress, Dominance applies its final integer division by ten, competition
  ties retain player-slot order, and eliminated players trail unranked.
- Control conflicts now use one phase-opening owner and defense snapshot for all
  player groups in a sector. The binary-recovered candidate list chooses equal
  positive leaders randomly in ascending player-slot order; at zero margin it
  places neutral/no-capture before every tied player. The recorded one-based
  roll preserves that ordering, a controlled sector can be overthrown only once,
  and an execution-time Crackdown rejects every competing group.
  Independent sectors resolve in ascending board order, while each player's
  participants retain persistent roster order.
- A complete owner-field scan of the original whole-turn resolver found writes
  only for the third-Crackdown neutralization and a successful Control winner.
  Moving or terminating the last friendly gang therefore leaves sector ownership
  intact; dedicated fixtures now prevent accidental auto-abandonment.
- Movement now mirrors the binary's two fixed scans: every Terminate resolves
  first, then Moves resolve by player and persistent roster slot, so destination
  capacity contention no longer depends on submission order.
- Combat attacks and the following police pass now each consume RNG in fixed
  player/roster-slot order. Reversed-submission gang attacks and a deliberately
  non-ID-sorted police roster guard both recovered scans.
- Crackdown history now matches the original two shorts exactly: entries expire
  only below `current turn - 5`, the third retained trigger writes the current
  turn to both slots after neutralizing control, and only then does the resolver
  draw and add 3-5 police turns. Inclusive-boundary, reacquisition, cleanup, and
  duplicate-slot save round-trip fixtures cover the recovered behavior.
- Control and Chaos regression fixtures now make density-derived sector Income
  differ from summed site Cash, guarding the recovered distinction. Chaos adds
  sector Income separately to every participating gang's pool, rolls gangs in
  fixed player/roster-slot order, then groups successes by player and sector.
  Uncontrolled half income is divided once after that aggregation; reversed
  submission and RNG-state fixtures guard the recovered ordering.
- Give now uses the original `PX05015` Equipment to Give panel before its
  compatible same-sector recipient list. One command can carry any combination
  of the source gang's exact three equipped items. Transactions now scan fixed
  player and roster slots, reserve outgoing items, and apply incoming gifts only
  after recipient transactions, allowing swaps and overwriting same-turn buys
  exactly as the resolver does. Native saves are v19, replays are v21 and canonical hashes
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
4. Complete remaining sound triggers and native audio/music validation.
5. Decide and implement Smacker playback or extractor-side transcoding; capture
   native WinHelp typography/paragraph geometry only where pixel-viewer fidelity
   materially benefits from it.

For authoritative scope and parity status, continue with
[IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md) and
[PARITY-MATRIX.md](PARITY-MATRIX.md). Record new static findings in
[ORIGINAL-INTERNALS.md](ORIGINAL-INTERNALS.md), intended mechanics in
[GAME-RULES.md](GAME-RULES.md), and player-visible AI behavior in
[AI-SPEC.md](AI-SPEC.md).
