# Manual validation plan

Work that needs a person to run the original game (BLD-GOG-EN-1.1, the GOG release) and watch,
capture or play it. Nothing here is done until the maintainer schedules a session. Each item
names the spec entries it concerns, the starting state, and what to record.

A run is recorded as a dynamic finding (`FND-*`, `method: dynamic`, with its environment) or, when
a test is to replay it, as an experiment (`EXP-*`) with a fixture, as the documentation standard
describes. Screenshots, video, audio and saves that hold the game's content are not committed:
their xxh3 hashes go in the entry, and the files go in `GAME_DIR/captures/`, named by hash. With
a dynamic finding beside a static one, an entry can become `established`. Delete an item once its
finding or experiment is in the spec.

## Executable, platform and file formats

- FND-PLATFORM-008, FMT-GFX-001: do the two white-keyed images show the
  exact-white pixels as transparent and near-white pixels as opaque? Start a
  match in 16-bit colour, open the screens that draw those two images, capture
  the window and compare the pixels at the key boundary with the file data.
  Record the captures and the pixel values on both sides of the boundary.
- FMT-GFX-001, FND-GFX-003: how are the odd-width images (`PX00202`,
  `PX00203`, `PX06008`) drawn? Reach the end-of-game screens and the report
  illustration that uses `PX06008`, capture them, and measure the drawn width
  and whether the last column is repeated, cut or shifted.
- RULE-GFX-001: does the 8-bit set draw the same pixels as the rule's decode?
  Run the game in 8-bit colour, capture one screen per RLE8 image family, and
  compare with the decoded file. Record any pixel that differs.
- BUG-SAVE-001, FMT-SAVE-001: what happens after loading a truncated save?
  Save a match, copy the file, cut it to a length inside block 2 (for example
  20,000 bytes), load it, and record the message shown, the screen reached,
  and whether the match can be played on with mixed state.
- FMT-SAVE-001: do real save files match the layout? Save at the start of a
  match and after a few turns, and compare the file sizes (45,305) and the
  opening and closing markers; record the files for later block checks.

## Shared in-memory structures

- FMT-STATE-001, FMT-STATE-002, FMT-STATE-003: the S40W save file holds the
  gang, sector and combat-record blocks verbatim (FND-PLATFORM-003). Start a
  local game, play two turns in which gangs attack, move and influence, and
  save. Decode the gang block with fmt_state_001, the sector block with
  fmt_state_002 and the combat-record block with fmt_state_003. Record, for a
  few gangs and sectors, the values the panels show next to the decoded bytes:
  each gang's Force, definition (its portrait), owner and all fourteen
  statistics; each sector's Income, Tolerance, Support and owner-only Cash
  rows; for combat records, the Force bars in Detailed Combat. This settles the
  sourced rows (`player`, `definition`, `force`, the statistic order, the site
  bonus order), the disputed sector `income` byte and the disputed combat
  record byte 0.
- FMT-STATE-002 `crackdown_turns`, glossary `crackdown_history`: in the same
  kind of save, trigger a Crackdown in a sector and save on the next two turns.
  Record the sector's +0x0F byte and the two INT16 values at 0x004ABCC0 +
  sector * 4 in each save.
- FMT-STATE-005: send a Comlink message in a hot-seat game, save, and decode
  the message block; record the text bytes after the message ends and the
  value of byte 0xA5.

## Random numbers and the order of a turn

- RULE-RNG-001, RULE-RNG-002: does the raw sequence match the prediction?
  Start the original under a debugger, break in `fn_00478CC0` and record its
  argument (the seed). Then log the argument and result of every call of
  `fn_0045D227` up to the first planning phase of a new local game with fixed
  setup choices. Record whether the startup `serialNum` draws happened, the
  seed, and every (argument, result) pair; compare with RULE-RNG-002 run from
  the seed.
- RULE-TURN-001: is Upkeep skipped on the first turn after loading a save? Play
  a local game to turn 3, save during planning, note each player's cash, quit
  and load the save. Record each player's cash at the first planning phase
  after the load and at the next one, with the gang Upkeep and sector income
  the finance panel shows.
- RULE-HIDE-001, RULE-TURN-004, RULE-TURN-005: when does a gang start and stop
  hiding? Hot-seat game with two human players whose gangs share a sector.
  Player 1 gives a gang a one-off Hide; player 2 attacks it. Next turn repeat
  with nothing changed (the Hide has expired), then with a recurring Hide, then
  replace the recurring Hide with Chaos during player 1's planning. Record for
  each step whether the attack needed a hidden-target roll (Last Turn report,
  Detailed Combat) and whether the target could retaliate.
- RULE-TURN-006: elimination order and reports. Reach a turn in which two
  players are eliminated at once, one of them in Eliminate by losing the Right
  Hands. Record each player's Last Turn reports in order, the retired sectors'
  owners and site progress, and whether the match ends that turn.
- RULE-TURN-002: runtime check of the step order. In one turn give a Chaos
  order that triggers a Crackdown in a sector where another gang of the same
  player sits, an Equip that only a same-turn Sell can pay for, and a Terminate
  and a Move of two gangs into one sector. Record cash after the turn, whether
  the police attacked in the new Crackdown sector that turn, and the Last Turn
  report order.

## Hire, Influence, Research, Bribe, Snitch, Tolerance and sites

- RULE-HIRE-001, RULE-HIRE-002: from a fixed save, hire one offer and snub
  none, end the turn, and record the new gang's Force and the replacement
  offer; repeat many times with the seed varied. Record the distribution of
  Force (expected 5 to 9, uniform) and whether the replacement ever equals the
  other two offers or the gang just hired.
- RULE-HIRE-001: order a hire into a sector holding six of an opponent's
  gangs and none of one's own; record that it succeeds. Order a hire into a
  sector holding six of one's own gangs, then order one of them to Move away;
  record whether the hire succeeds (it should, since Move resolves first).
- RULE-HIRE-001: with cash one below a gang's cost at the start of the turn
  and a Sell by an earlier roster slot, record whether the hire succeeds; with
  exactly the cost, record success and the new cash.
- RULE-HIRE-003 / SCR-HIRE-002: capture the dock after a drop, after Reject,
  after a second Reject, and after dropping a snubbed offer; record which
  sectors accept a drop. Capture positions of the portraits, the Reject
  control and the stamps.
- RULE-INFLUENCE-001: from a fixed save, have two gangs of one player
  influence the same site in the same turn with a Resistance the first gang
  can meet; record the progress after the turn and whether the second gang's
  roll shows. Then have an opponent take the sector and retake it; record the
  site's progress.
- RULE-HEAL-001: heal a gang at Force 10 and at Force 9; record the Force
  after and, where the generator state can be read, whether draws were made.
- RULE-RESEARCH-001: two gangs of one player researching the same item that
  the first can finish; record the remaining research and the report.
- RULE-BRIBE-001 / BUG-BRIBE-001: bribe a sector with Tolerance 39 twice in
  one turn; record 45 (no cap) and cash down by 6. Bribe with 2 cash; record
  the report and that nothing changed.
- RULE-SNITCH-001 / RULE-TOLERANCE-002: Snitch a sector at Tolerance 2 while
  in debt; record Tolerance 1 after the turn and no cash change.
- RULE-TOLERANCE-001: after one Bribe, record the sector's Tolerance at each
  of the next four turn starts and, if it can be seen, at each phase, to find
  when the one-point return happens and what normal is.
- SCR-HIRE-001, SCR-INFLUENCE-001, SCR-RESEARCH-001: unscaled 640x480
  captures of each panel open, with the pointer over each control, to pin
  the panel origins and element positions.

## Movement, Control, gangs, equipment and money

- RULE-EQUIP-001, RULE-EQUIP-002, FND-EQUIP-006: Equip at the cash boundary.
  Start: a save with one gang able to buy an item of known price. Queue the
  Equip with cash exactly equal to the price, then $1 short, then $1 short with
  a Sell queued on an earlier roster slot of the same player, then with the
  Sell on a later slot. Record cash after the turn, the gang's items and the
  Last Turn report.
- RULE-EQUIP-003: Factory price. Start: a player owning a sector with a
  completed Factory. Equip an item whose Cost is not a multiple of 3 from a
  gang in that sector and from one outside it. Record cash before and after.
- RULE-CONTROL-001: Control with zero margin. Start: a neutral sector whose
  Income equals the pooled Force plus Control of one challenger; then two
  challengers from different players at equal strength. Run 20 turns each from
  the same save with different seeds. Record who owns the sector afterwards and
  the reports.
- BUG-CONTROL-001: a nonparticipant winning Control. Start: a sector whose
  defense sum is negative (if one can be arranged, for example through negative site Support), one
  challenger with a Control order and a second player with a gang in the city
  but no order there. Record the owner after the turn over several runs.
- RULE-MOVE-002: Move contention. Start: a sector holding five of one player's
  gangs, and two of that player's gangs in neighbouring sectors both ordered to
  Move into it. Record which gang moves and which stays, then repeat with the
  roster slots swapped.
- RULE-SELL-001, BUG-SELL-001: multi-item Sell. Start: a gang holding a weapon,
  armor and a miscellaneous item of known Cost. Sell two of them, then all
  three. Record cash and the gang's items after each.
- RULE-GIVE-001: same-turn swaps and overwrites. Start: two gangs in one
  sector, each Giving its weapon to the other, and a third gang Equipping a
  weapon while receiving one by Give. Record the items after the turn.
- RULE-UPKEEP-001: upkeep around zero cash. Start: saves near $0 with a known
  number of owned sectors, completed sites with positive and negative Cash, and
  gangs of known Upkeep. Record the Financial panel before the turn, cash after
  Upkeep, and the Cash Earned and Cash Spent statistics at the end of the game.
- RULE-GANG-002, RULE-TERMINATE-001: Terminate a gang holding items, save, and
  compare the gang record in the save before and after (sector byte, items,
  Force, orders).
- SCR-MOVE-001, SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001, SCR-FINANCE-001,
  SCR-GANG-001, SCR-GANG-002: capture each panel at 640x480 with Slide Panels
  on and off; record the control positions, the keys that confirm and cancel,
  the selection markers, which Finance row holds which amount, and the opening
  and closing sounds.

## Attack, combat, detection, Chaos and police

- RULE-ATTACK-001, RULE-COMBAT-004, FND-COMBAT-006: do two gangs that attack
  each other produce two clips and two retaliation rolls? Start: a save with
  one gang of each of two human players in one sector, both able to see the
  other. Give each an Attack on the other, end the turn with Detailed Combat
  on. Record the clips (video), the end-of-turn Force of both, and the
  combat results panel. Repeat 20 times with different seeds.
- RULE-COMBAT-004: clip order in a sector with several of the viewer's gangs.
  Start: three of the viewer's gangs in one sector (roster slots known), each
  attacking or attacked by different enemy gangs, and police present. Record
  the clip order and the bars between clips.
- RULE-ATTACK-001, RULE-COMBAT-001: attack formula matrix. Start: fixed
  attacker and defender pairs, bare handed, with melee, blade and ranged
  weapons, with and without Martial Arts, and a target ordered to Hide; human
  players (band 1) and computer players at Goon and Crime Lord (bands 0 and 2).
  Record end Force of both gangs and Damage Inflicted over many runs; compare
  the distributions with the rule.
- RULE-POLICE-001: police detection and damage boundaries. Start: gangs in a
  sector with police, visible at Stealth 2, 3, 4, 22 and 23, hiding at Stealth
  18 and 19, with Defense 24 and 25. Record which gangs take police damage over
  many turns.
- RULE-CHAOS-001, RULE-CHAOS-002: Chaos at the Tolerance boundary. Start:
  otherwise identical saves, controlled and uncontrolled sector, Chaos totals
  just below, equal to and above the Tolerance. Record cash, the Crackdown, the
  police presence and the reports.
- RULE-POLICE-002, RULE-POLICE-004: Crackdown reports and neutralization.
  Start: a sector with gangs of two players (one not ordering Chaos) and a
  third player who owns it with no gang there; force Crackdowns in three turns
  within five. Record which players get which Last Turn reports, and the
  sector's owner and sites afterwards.
- RULE-POLICE-003: count the police Combat phases after a Crackdown (expect 3
  to 5 counting the Crackdown's own turn).
- BUG-COMBAT-001: reproduce the reported freeze with Detailed Combat on, on a
  known installation. Record the system, the battle, and where it stops (CPU
  use, sound, window messages).
- SCR-COMBAT-002, FND-UI-001: capture a Detailed Combat clip at 640x480 with
  frame timestamps to measure the 166 ms cadence, the white flashes and the
  hold; capture the Force tracks to pin their x positions.
- SCR-ATTACK-001, SCR-COMBAT-001: capture the pickers' pressed states, the
  target marker and the panel origins.
- FND-COMBAT-005: the old text mentions "a controlled check of the original
  game on 2026-09-25" in which a retaliation dealt its damage inside the
  attacker's animation. It was not recorded with an environment or capture
  hash. Repeat it and record it as a dynamic finding or an experiment.

## Last Turn Events, Comlink and Search

- SCR-EVENT-001, RULE-EVENT-005: capture the Last Turn Events panel at 640x480
  for a turn with a Crackdown, a sector capture, a completed site, a completed
  item and a cash failure. Start from a new hot-seat game with two humans and
  set up each event over a few turns. Record each page, the positions of text
  and art, and whether the panel opens by itself.
- SCR-EVENT-001: close the panel before the last page and record whether the
  Events button blinks, and what stops it (manual page 23).
- RULE-EVENT-002: produce more than 32 reports for one player in one turn (for
  example many failed hires and a Crackdown) and check that the first 32 are
  kept in order.
- RULE-EVENT-003: eliminate a player in a hot-seat game and record which slots
  see the report and in what order.
- RULE-EVENT-004: move a gang out of a sector in the turn it is cracked down on
  and check that its player still gets the report.
- SCR-COMLINK-001, RULE-COMLINK-004, RULE-COMLINK-005: in a two-human game,
  send 17 messages to one player; record which messages remain, where View
  opens, the date shown, and when the unread alert (`DATA/Snd00205`, every four
  seconds) stops.
- SCR-COMLINK-002, RULE-COMLINK-006: record the Send panel while typing,
  including Backspace, Enter on the last row and typing past column 39; time
  the caret phase (expected 498 ms); record the pressed faces of Cancel and
  Send and the order of the recipient cells.
- SCR-SEARCH-001, RULE-SEARCH-002: capture the city before and after selecting
  site types in Search; check marker colours, stacking and transparency;
  double-click a row and record the Site Information panel; save, reload and
  record whether the filter survives.

## Setup, city generation, objectives and awards

- RULE-SETUP-001, RULE-SETUP-004, RULE-CITY-001 to RULE-CITY-004: capture an
  initial-state fixture. Start a local game with one human and default
  settings, save on the first planning turn, and record the save file, the
  seed if a debugger is attached, and a screenshot of the city. Repeat with
  Armageddon to check the $500 start and the site exclusions.
- RULE-SETUP-001, RULE-SETUP-005, RULE-SETUP-006, RULE-SETUP-007: for each
  name modifier, start a game with one human named with it; record starting
  cash, the Crackdown state of neutral sectors, the roster, and whether enemy
  gangs are visible on turn 1.
- RULE-SETUP-009, SCR-SETUP-001: on the setup screen, confirm an empty name
  in the name editor and record the resulting name; drag a face onto an empty
  cell and onto another human; take screenshots.
- SCR-SETUP-001: record the rejected-input sound when pressing Add with six
  players and Remove with one, and whether any key works on the screen.
- RULE-SETUP-008, SCR-SETUP-002: in a two-human hot-seat game, with Detailed
  Combat off and then on, record the order of the Ready card, Game
  Information, combat panels, Last Turn Events and the Comlink alert over two
  turns.
- RULE-OBJECTIVE-005, SCR-OBJECTIVE-002: in a two-human game, get one human
  eliminated while the other survives; capture the handoff and elimination
  cards and note the music change.
- RULE-AWARDS-001, RULE-AWARDS-002, SCR-AWARDS-001, SCR-AWARDS-002: finish a
  one-human game by victory and a two-human game; capture the splash and both
  tabs of the results, and note which tab shows first and the awards given.
- BUG-OBJECTIVE-001: in Greed, run a player's cash below -32000 with one
  player eliminated and open the Player Rankings panel; record the portraits'
  heights.
- BUG-AWARDS-001: finish a match in which no player spent cash and one player
  leads Overthrows and damage; record how many icons that player's row shows.
- SCR-NET-004, SCR-NET-005: if a legacy network game can be set up between
  two machines, capture the transfer and synchronization frames and time the
  spinner.

## Computer players

- RULE-AI-019 to RULE-AI-031: do the family handlers plan as written? Start a
  new six-player match with one human and five computers at each Mentality,
  with a debugger breakpoint after the dispatcher for each computer gang, and
  record each gang's family byte, previous and older actions, the planned
  action and target bytes, and the RNG state before and after. One trace per
  scenario covers the scenario-specific families (10, 11, 12 in Siege; 13, 14
  in Big Man and Eliminate).
- RULE-AI-003, RULE-AI-014 to RULE-AI-017: does hostility follow the attitude
  rules? In a Criminal match, attack one computer player's gang once, then
  read the attitude matrix at `0x004AB590` before and after resolution and for
  the next ten turns; record the cell values and when the computer first
  attacks the human.
- RULE-AI-018: do the difficulty bands change the dice? Read the table at
  `0x004A2570` after a new match at each Mentality, with the human and computer
  slots recorded.
- RULE-AI-013, BUG-AI-002: where do computer hires land? Record the anchor at
  `0x0048E2F8` and the sector of each computer hire for twenty turns; try to
  reach a computer player with no owned sector with room and record where its
  next hire goes.
- BUG-AI-001: in Dominance, does a computer player hire family-6 gangs on
  consecutive hires? Record `0x00482128` and `0x00482160` and the hired gang's
  family each turn for fifty turns.

## Screens, options, planning timer and sound

Checks that need a person running the original.
- FND-UI-010, FND-UI-001, RULE-UI-008: record the window capture of the
  Detailed Combat presentation again with a frame counter, to confirm the
  166 ms phase tick and give the capture a file hash. Start a new game with
  Detailed Combat on, attack a visible enemy gang, capture at 30 fps or more,
  and record the frame times at which each animation phase changes.
- RULE-TIMER-001..003: pick a 30-second planning limit, start planning, and
  record with a stopwatch or capture: the bar's width over time, how often it
  redraws, when slots 7 and 8 sound, and what happens at expiry with and
  without a panel open and with idle gangs (does SCR-OPTIONS-001 appear?).
- RULE-AUDIO-008, RULE-AUDIO-007: in a hot-seat game, send a Comlink message to
  the next player; on their turn, time the alert repeats and check when they
  stop (after opening Comlink, or after viewing the last unread message).
- RULE-UI-003: with Slide Panels on, capture a panel opening and closing;
  record the step count, the duration and whether the panel moves whole or is
  uncovered. Repeat on a slow machine setting if one is available.
- RULE-AUDIO-001..003: check music programs on the title, in game and at the
  endgame; set Music to 0 and back to a nonzero level and note whether music
  restarts; lose and regain focus.
- RULE-AUDIO-006, BUG-AUDIO-001: set Sound Effects to 0 and play a local game;
  record whether the turn-start cue sounds.
- RULE-OPTIONS-001/002, BUG-OPTIONS-001/002: change options, quit, restart, and
  check whether they persist; with a registry key created by hand with some
  values missing, check which defaults the game ends up with.
- SCR-UI-001, SCR-UI-009: whether the logo and intro movies play on every
  start, whether a key or click skips them, and which menu items are disabled
  on the title screen and during resolution.
- SCR-UI-005..008, SCR-OPTIONS-001: try Enter, Escape, `VK_EXECUTE` (if a
  keyboard sends it) and right-click on each panel, and clicks outside the
  faces; record what each does and which sound plays.
- SCR-UI-008: confirm that Game Information opens by itself at the start of a
  multi-player game and after loading a save.
- RULE-UI-007: note when the hourglass appears (setup, loading, resolution).

## Found while integrating the spec

- RULE-MOVE-001, SCR-MOVE-001 (DEV-MOVE-001): in the Move panel, pick a destination sector that
  already holds six of the player's gangs. Record whether the panel refuses it (and with which
  sound), or accepts the order. If it refuses, DEV-MOVE-001 is dropped.
- SCR-UI-004 (DEV-UI-014): does the detailed-sector screen show the remaining police turns
  anywhere? Open a sector under a Crackdown and capture it.
