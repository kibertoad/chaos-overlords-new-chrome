# Development handover

Status: active at a validated local checkpoint
Last updated: 2026-09-13

## Repository state

- The canonical repository is
  `https://github.com/kibertoad/chaos-overlords-new-chrome.git`. `main` contains
  every accepted checkpoint through player-approved online AI takeover and pnpm 11 tooling. New cohesive batches are
  committed and pushed directly to `main`; older `codex/full-reimplementation*` refs remain only as
  historical checkpoints and are not the active integration path.
- The canonical local gate is `./tools/Invoke-Validation.ps1`: its fast default
  passes 1,522 focused tests in about 35 seconds with zero warnings. Use
  `-IncludeLongRunningTests` for the complete 1,575-test gate. The 53 repeated AI campaign cases last
  passed independently in 8 minutes 35 seconds and are tagged
  `LongRunning`; focused planner/policy/headless/replay checks remain in default.
- Validation deliberately stops only a development `Rechaos.Game` executable
  located inside this checkout, serializes concurrent validation attempts, and
  caps MSBuild at two workers. It retains incremental outputs and compiler/build
  server reuse. Use `-ShutdownBuildServersAfterRun` only to clear stale servers;
  it can also make the next IDE build cold.
- Native saves are format v23, replays are v27, canonical hashes are v26, asset
  manifests are v6, extracted help is v3, and client preferences are v8. Save
  and replay compatibility may intentionally break before 1.0.0; retain the
  migration/versioning machinery for post-1.0 compatibility.

## Latest playable work

- Fresh New Game setup now defaults to Kill 'Em All, matching the first stable
  original-runtime capture. Five burst frames agreed byte-for-byte and the
  current user and machine registry contained no `prefsObjective` override.
  The earlier static analysis correctly recovered initialized value zero but
  incorrectly mapped it through the recreation enum instead of the original
  visual-button order.

- Online AI takeover is player-approved end to end. A departure or wholly missed timed turn opens
  a visible unanimous `WAIT`/`USE AI` vote; waiting preserves the human controller indefinitely,
  authenticated returning activity atomically cancels a pending absence, and only
  `match.playerTakenOver` and `match.playerReturned` record replay-v27 controller transfers at a clean Command boundary.
  Reconnect replays votes, approved transfers, and seals gaplessly in authoritative order.

- `Rechaos.Tools ai-tournament` now drives presentation-free six-computer
  matches directly through the authoritative model with bounded parallelism,
  deterministic consecutive seeds, selectable Original/Advanced policy and
  scenario sets, five-second heartbeats, optional turn/boundary/event traces,
  stable JSON results, and configurable replay sampling. The shared core runner
  never constructs graphics, audio, input, animation, or real-time pacing. A
  12-match Advanced objective smoke sample through turn 15 completed with zero
  failures and replay-verified 3/3 sampled matches; trace output made each live
  match and its progress visible throughout the 13.4-second run.

- Options now exposes a persistent, default-off Advanced AI default for future
  new matches, with explicit hover and F1 documentation that active and loaded
  matches retain their stored policy. Save-slot details and the Report Bug panel
  identify that stored policy, and bug-report context sends it as a triage field.
  Advanced composes small transformations over the single
  Original planner: deterministic legal recovery for idle gangs, plus a
  Crime-Lord/Homicidal expansion rule that moves healthy gangs outward when
  they would remain idle or repeat Hide, Snitch, or Bribe. Goon and Criminal
  retain their more passive cadence. The policy is persisted and authenticated
  by local saves, replays, hashes, and online settings.
- Paired 12-seed, 15-turn Power playtests isolate both policy passes. Criminal
  idle recovery reduced idle gang-turns from 242 to 128, increased controlled
  sector-turns from 3,126 to 3,132 and final controlled sectors from 365 to 372,
  while undefended-sector turns remained 664. Crime Lord expansion reduced idle
  turns from 258 to 210, increased outward moves from 402 to 476, controlled
  sector-turns from 3,315 to 3,557, final controlled sectors from 399 to 433,
  and defended controlled-sector turns from 2,493 to 2,578. Independent seed
  pairs run on two workers and report timings separately from replay-heavy
  parity tournaments.
- Native restore now rejects notification records dated after the restored
  turn, references whose event turn/phase boundary does not match the
  notification, and gang references absent from the restored roster. These
  checks run before accepting even legacy hashes, closing another projection
  path for structurally impossible histories.
- Options now exposes a keyboard/mouse diagnostics export. It creates a unique,
  atomically promoted ZIP under local application data with allowlisted session
  events and path/message-free crash summaries. Raw reports stay local, legacy
  exception strings are reduced to type names during export, new diagnostic
  error fields record only types, and all export failures remain non-fatal.
- Both original Smacker-v2 movies now pass a bounded structural parser before
  source-pack acceptance. Exact 480x256 geometry, 100 ms cadence, 200/1,150
  frame counts, 20/115-second durations, packed 22,050 Hz 8-bit mono/stereo
  descriptors, and byte-exact container extents are pinned. Physical frame
  descriptors and bounded palette/audio/video demultiplexing are implemented.
  Palette state, packed unsigned 8-bit mono/stereo audio, the four canonical
  codebooks, and all 1,350 indexed video frames now decode in managed code
  across both complete movies. Startup now streams logo then intro at native
  centered size on a deterministic 100 ms timeline, submits converted PCM,
  pauses on focus loss, consumes explicit skip input, and fails through to the
  next movie or title. A bounded Windows run completed the logo and began the
  intro without diagnostics; no ambient codec is required.
- The movies are no longer an unattended toll on every launch. Completing the
  queue records `IntroMoviesSeen` in preferences (format v8, migrated from v7
  with the intro still owed once), so only an installation that has not shown
  them yet streams them at startup. The title screen carries an `INTRO` button that replays the queue at
  any time, reports an unreadable pack instead of stalling, and hands the menu
  music back when the last movie ends.
- The embedded gameplay tables now have a reproducible extractor command:
  `--generate-game-data <json> --source <install>`. Format 1 validates the
  fingerprinted legal source, deterministically reproduces the pinned JSON,
  and promotes it atomically. The original-table reader and embedded loader
  share semantic validation for counts/order, names, special-site roles, item
  categories and media bounds, and all eleven unused item sentinels.

- Last Turn Events retains each fully reviewed batch for later reopening
  through Events while dismissing its unread blink. Site-cooperation reports
  use the statically recovered centered 94-by-62 site crop, native
  `COLORONCOLOR`-equivalent stretch, and resource-146 25-percent ordered mask
  by default; Options persists a Smooth alternative that linearly filters the
  unmasked background. The
  template's white DATE/OBJECT/STATUS labels remain intact and dynamic green
  values use their measured baselines.

- Upkeep and both Finance panels preserve the executable's actual sector cash:
  flat $1 tax plus influenced-site Cash. Static caller tracing confirmed that
  the original combines these in a recomputed byte before playable turns;
  generated Income is a separate field and is not recurring sector tax. The
  guarded first outer-loop pass now also leaves setup cash unchanged before
  initial planning.
- Fresh local setup now implements every recovered rule-changing exact name.
  `SMGSPANK` adds five Force-10 Right Hands; `SMGKICKASS` adds five Force-10
  GROUND ZERO gangs with the recovered top-tier weapon, armor, and miscellaneous
  loadout; and `SMGHUBBLE` gives its player global opposing-gang visibility.
  These add no setup RNG calls, and all six magic names are neutralized online.
- `Rechaos.Extractor --verify-output --json` now emits a stable schema-v1
  automation report with quick/full mode, expected/actual format and file
  counts, verified count, and categorized code/message/path/expected/actual
  diagnostics. The
  human output and exit-code contract remain unchanged.
- Local-game RNG startup now matches the executable's unique seed path: capture
  the process-uptime millisecond clock at game construction and zero-extend only
  its low 16 bits. Explicit deterministic replay, test, and multiplayer seeds
  remain full-width by design. The inclusive native wrapper also clamps bounds
  below one to one while still consuming all three raw draws.
- Execution now mirrors the original resolver's split Chaos scheduling. Chaos
  rolls and Crackdown creation occur immediately after Instant so newly arrived
  police attack in the same turn; Transactions still precede the delayed Chaos
  cash/statistic payout. Prepared outcomes survive a save between those passes,
  and the common police-duration tick leaves two to four future Combat phases.
- Asset verification now rejects every unmanifested output even in quick mode.
  An obsolete file therefore prevents the already-complete shortcut and the
  existing rollback-safe whole-directory promotion removes it; incomplete or
  dirty staged packs still cannot replace a valid installed generation.
- City and Sector Financial now mirror the recovered multi-item Sell payout
  overwrite: the last selected equipment slot supplies the projected credit,
  rather than incorrectly using the primary selection.
- Online order readiness is now monotonic per turn even while a prior document
  is already in flight. A replacement draft can update the whole order document
  without accidentally retracting the player's earlier ready signal.
- A client that reaches an already-advanced online match now refreshes the
  authoritative match view, loads and verifies the newest compatible snapshot
  when one exists, replays every later sealed turn, restores its own current
  whole-document draft and ready state, then resumes the event stream at the
  refreshed sequence. Invalid snapshot hashes, draft schemas, and replayed
  operations fail explicitly instead of producing a partial local state.
- Crackdown combat now follows the executable rather than the manual's
  abbreviated detection table: visible detection is
  `clamp(115 - 5 * Stealth, 0, 100)`, Hide subtracts 20 percentage points, and
  detected police roll Force 5 + Combat 20 minus effective Defense dice at 5+.
  Exact probability and dice-pool boundary fixtures guard both formulas.
- In-game Help now adds recovered executable notes directly to their matching
  Game Settings, Attack, Bribe, Chaos, Control, Equip, Heal, Hide, Influence,
  Research, Sell, Snitch, and Crackdown subjects. These cover the implemented
  high-confidence difficulty, pool, threshold, combat, transaction, and police
  formulas; missing context anchors fall back to subject titles, and a missing
  subject becomes a clearly named listed entry instead of a catch-all page.
- The `PX00132` hot-seat handoff now draws the incoming player's Overlord in
  its measured 80-by-77 portrait aperture instead of leaving the frame black.
- Combat Results now follows the original sector-indexed table instead of
  paging individual events. Sectors appear in board order, occupied sectors
  reveal other players' fights, both force apertures use the recovered
  two-by-three gang grid, all five opposing-player slots retain their fixed
  order and inactive dimming, and selecting a populated opponent uses the
  recovered slot-3 cue before Detail replays that selected result.
- Combat Results and live Combat fit the native 54-by-52 sector image into the
  original map aperture and leave the sector code below it. `PX05012` force
  grids and opponent portraits use the recovered renderer origins; `PX05014`
  retains one baked, functional Cancel control instead of painting a duplicate.
- Hire, Ranking, and Combat Results entrances slide only their foreground panel
  over a stationary city/sector backdrop. Sector-detail End Turn now displays
  and routes the idle-gang warning modally instead of hiding it beneath the
  sector view, and Ranking cannot be opened through that warning.
- Detailed sectors display up to six gangs in the available two-column,
  three-row card grid. Handoff prepares replacement hire offers before any
  automatic reports, so every new dock portrait is visible without a first
  click. The one-off command list starts below its heading rather than under it.
- Enemy-controlled site progress uses the recreation's intentional violet
  highlight/center/shadow bevel; friendly and empty site tracks now use the
  original three-row green/red bevel and exact 100-pixel percentage arithmetic.
  A zero-resistance Headquarters with no explicit site influencer inherits its
  sector owner for this presentation, fixing the otherwise-green enemy HQ edge.
- Alt+Enter now toggles fullscreen alongside F11 and consumes Enter so it cannot
  activate the current screen as a side effect.
- Online Play fields now leave a clear gap between each preceding border and
  the next label. Its remaining recreation-only hosting/password/setup guidance
  and initial instruction status were removed; validation and connection
  failures still use the status line.
- The 432-by-416 ownership atlases place their visible grid at source `(4,3)`;
  its 54-by-52 sector crops advance by 53-by-51 because neighboring cells share
  their border pixels. The whole neutral atlas remains at city destination
  `(2,44)`, while ownership interiors, selection frames, hit-testing, and
  single-sector crops all use that measured grid origin and stride.
- The upper-right city console now provides hover explanations for Score, Cash,
  Sector, Income, Tolerance, Support, and Chaos. Cash displays its current
  signed whole-city Financial adjustment; the Income tooltip explicitly
  distinguishes the Chaos dice rating from the passive `$1` Sector Tax. The
  final template label is corrected from `CASH` to `CHAOS`, and its tooltips are
  composited above the Sector Details workspace rather than being clipped by it.
- Gang, Site, and Item Information now expose hover explanations for every
  statistic, including its actual dice/combat role and whether equipment or an
  influenced local site applies the modifier. Force, Upkeep, Tech, Resistance,
  Tolerance, Support, site Cash, and item Cost receive matching explanations.
  Item descriptions wrap within the native text aperture instead of touching
  its right border. The Hire comparison shares the gang-effect explanations;
  its baked zero placeholders are cleared before aligned, consistently colored
  values are rendered, including intentional two-digit Tech levels.
- Screen changes clear pending city/sector double-click state, preventing a
  previous sector click from leaking through a Ranking transition and opening
  Sector Details later. The console hit map also matches the artwork: the left
  `DETAIL` button opens Sector Details, while the complete 34-pixel right-hand
  `RANKING` row opens Ranking instead of its upper half being misrouted.
- Setup selection outlines now use the inset `PX00143` button faces instead of
  the broader hit rectangles that overlap section labels. The city/sector top
  bar uses the original portrait aperture and plays the twelve-frame
  `PX00129` active-player marker beside the current Overlord. Recreation-only
  navigation, drag, cancellation and success hints are suppressed while
  rejection reasons and genuine failures remain visible.
- Completed-Research artwork now occupies the exact 48-by-48 black monitor
  interior in `PX06005` instead of extending above and left of it. Static
  analysis confirms that the original does not re-center opaque item pixels,
  so visibly asymmetric frames such as Whip remain faithful. City and
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
- Recurring commands are limited in both the individual-gang picker and
  authoritative validation to Chaos, Control, Heal, Hide, Influence, and
  Research. Bribe and Snitch are one-off along with Attack, Equip, Give, Move,
  Sell, and Terminate. The native sector-wide recurring menu is narrower still:
  it omits Research.
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
- Endgame awards now use the native builder's priority and inclusive activity
  thresholds: Fist 5 Overthrows, Skull 50 direct Damage, and Chicken 10 Hides,
  followed by most/least Cash Spent. All six player slots and ties participate;
  outcome data keeps every award while each visible row shows the first three.
  The sole native Hide-counter write is unconditional, so a recurring Hide
  counts again each turn. Native active/recurring action-byte writes also show
  that recurring Hide remains hidden across Upkeep until replaced or cancelled;
  only one-off Hide expires at the boundary.
- Damage Inflicted now matches the sole native resolver write: every opening
  attack credits its complete computed damage even when it exceeds remaining
  Force or several attacks collectively overkill one target. Retaliation still
  awards no Damage Inflicted.
- Search now uses the original `PX05024` Search: Sites panel instead of the
  earlier incorrect detected-gang list. Its two-column aperture contains all 22
  site types with ALL/NONE and per-player individual toggles. The recovered city
  renderer uses `PX00150` to draw each controlled site unconditionally and each
  selected uncontrolled site, compacted to three marker slots per sector. A
  row double-click opens the site's definition information.
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
  authoritative replay-recorded operation. Opening the viewer and paging mark
  only the displayed record read through that same authoritative path; unread
  state remains until every retained unread record has actually been viewed.
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
  planning-countdown warnings use mapped sounds. Rejected command, equipment,
  Hire, Comlink, Events, Combat Results, and Gangs-in-Sector operations now use
  the original slot-4 cue; accepted submissions and standard panel
  confirmation/cancellation controls use slot 3 through the original shared
  helpers. Events, Combat Results,
  and incoming-Comlink paging is bounded rather than wrapping, with slot 3 on
  a legal step and slot 4 at the first/last-page boundary. General slot 9 is loaded
  by the original but has no call through its gated effect wrapper. Detailed Combat cues start with
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
  all objectives through live-equivalent 40-turn campaigns across five guarded
  seeds with offer refills, resolved hires, territorial expansion, scenario-
  specific combat/control signals, and replay verification. The Kill 'Em All
  guard respects the recovered rule that neutral all-computer Criminal matches
  do not invent hostility, while requiring attack activity whenever hostility
  exists. Big Man completes by turn 60 at one guarded seed; Kill 'Em All,
  Big 40, Eliminate, Siege, and Armageddon still need evidence-led completion
  policy, so that
  remains an explicit M6 gap. Eliminated planning slots are skipped through
  replay-recorded transitions, and negative effective Stealth is safely bounded
  to 100% police detection.
- Options defaults to Current gang statistics, Detailed Combat on, Slide Panels
  off, Warn If Idle Gangs on, Original Event Site Images, and Original AI for
  future new matches. The disabled panel-motion default is an intentional
  usability change from the recovered original default. Options persists those
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
  backup when replacing a corrupt primary. A verified backup now self-heals a
  missing or invalid primary through a separately flushed and validated recovery
  generation. Backup-only save slots remain visible, and both slot and replay
  recovery report whether the repair succeeded.
- Replay loading now enforces the recorded introduction version for every
  post-v2 authoritative operation. A relabeled legacy replay cannot execute
  later hire-offer, AI-planning, Comlink, or simultaneous-turn mutations.
- Native saves and replays now also reject tertiary or quaternary queued-command
  targets, submitted-command targets, and event targets under schema versions
  that predate those fields, closing a legacy-hash projection bypass.
- Sell now uses the original `PX05013` Equipment to Sell panel. Any combination
  of the acting gang's three equipped slots can be highlighted and confirmed in
  one command. The binary clears every selected slot but overwrites one payout
  local in weapon/armor/miscellaneous order, so compatibility resolution credits
  only the highest selected slot's half raw price, rounded down, without Factory.
- Factory integration now distinguishes activation from completion. An already
  active local Factory charges the recovered `Cost - trunc(Cost / 3)` price,
  while one completed during Instant does not discount a same-turn Transaction
  Equip and activates at the next planning boundary. The odd-priced $11 Katana
  therefore costs $8 only with a previously active Factory. Manual-backed
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
- Correction: later static analysis separated the executable's generated
  3-7 **Income** byte from its recomputed `$1` plus influenced-site **Cash**
  byte. Control and Chaos use generated Income; the current recreation and its
  regression fixtures incorrectly use Cash. Chaos otherwise rolls
  gangs in fixed player/roster-slot order, then groups successes by player and sector.
  Uncontrolled half income is divided once after that aggregation; reversed
  submission and RNG-state fixtures guard the recovered ordering.
- Completed Influence remains pending through the rest of its execution turn.
  The next pre-planning rebuild activates ownership, Support, Tolerance, Cash,
  special-building, and local gang-stat benefits in sector/site order.
- Give now uses the original `PX05015` Equipment to Give panel before its
  compatible same-sector recipient list. One command can carry any combination
  of the source gang's exact three equipped items. Transactions now scan fixed
  player and roster slots, reserve outgoing items, and apply incoming gifts only
  after recipient transactions, allowing swaps and overwriting same-turn buys
  exactly as the resolver does. Native saves are v22, replays are v24 and canonical hashes
  are v25; the immediately previous formats
  remain readable through their preserved fingerprint projections.
- Canonical hash v24 authenticates the complete ordered event history, including
  nested command, economy, hire, police, objective, and outcome facts. Native
  phase-boundary history. Save v21 and replay v23 retain explicit v20/v22
  compatibility projections through canonical hash v23.
  Event records and their nested collections are frozen on append, while their
  exact canonical bytes are append-cached so repeated boundary hashes do not
  re-encode the complete prior history. Restore requires the unpruned log's
  exact contiguous sequence and rejects event kinds whose attached detail
  payload does not match, including under legacy pre-event-body hashes.
  Notification and Comlink restore likewise require the contiguous queue
  suffixes their bounded runtime operations can actually produce; notification
  phase/reference shapes and human Comlink senders are validated before use.
  Completed outcomes are deeply frozen and restore only when their scenario,
  participants, standings, awards, and sole `MatchEnded` event agree.
  Replay steps now require the exact payload fields for their operation kind;
  irrelevant known fields are rejected rather than silently ignored, nested
  Comlink recipient lists are frozen, and schema-introduction errors retain
  diagnostic precedence.
- Move now uses the original `PX05006` Movement panel. Its destination aperture
  is an exact 3-by-3 composition of the city map's native 54-by-52 ownership
  tiles around the acting gang, with mouse and directional-key selection limited
  to the authoritative adjacent-sector command options.
- Fresh Siege matches now designate all six assigned starting HQ sectors as
  important, matching the manual's setup rule, and the city renders two gray
  pylons in each objective tile. Static renderer analysis later replaced the
  approximation with the exact white-keyed `PX00129` pylon sprite and established
  that Big Man uses the same overlay on sectors 27, 28, 35, and 36.

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
4. Validate the recovered per-record Comlink acknowledgement and slot-6 cadence
   at runtime, then validate effect overlap/interruption and complete native
   audio/music validation. The four-second repeat, slot-2 inventory, and
   Combat-selection slot-3 call are statically classified and routed.
5. Capture original startup-movie trigger/skip behavior and validate native
   video color/audio fidelity on each supported platform; capture native WinHelp
   typography/paragraph geometry only where pixel-viewer fidelity materially
   benefits from it.

For authoritative scope and parity status, continue with
[IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md) and
[PARITY-MATRIX.md](PARITY-MATRIX.md). Record new static findings in
[ORIGINAL-INTERNALS.md](ORIGINAL-INTERNALS.md), intended mechanics in
[GAME-RULES.md](GAME-RULES.md), and player-visible AI behavior in
[AI-SPEC.md](AI-SPEC.md).
