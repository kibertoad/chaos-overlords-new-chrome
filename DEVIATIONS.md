# Deviation log

Every place the rebuild departs on purpose from the [spec](spec/README.md), as version 1 of the
[documentation standard](https://dinorefurb.com/documentation-standard/#deviation-log) defines
the log. Each entry names the rule, format, screen or bug it departs from, so it appears in that
entry's row of [PARITY.md](PARITY.md). IDs are never reused or renumbered; an entry that is
dropped keeps its heading and gives the date and reason in its Dropped item.

Default is `off`, `on` or `mandatory`, as the standard defines them. A setting starts `off`, with
the original's behaviour, unless the entry's Justification argues that the rebuild's behaviour is
strictly better; then it starts `on`, and a player who wants the original switches it off. A
deviation with no setting is `mandatory`, and its Justification also says why the original's
behaviour is not worth a setting. The validation suite runs with every setting switched off, and a
test that reaches a mandatory deviation cites its ID and allows for it. Default always describes
the deviation, never the option it is carried by: when the deviation is to start an option off
that the original starts on, the Setting item says the setting is inverted and which value is the
original's.

A `mandatory` deviation may have a Replaces item directly after Departs from, naming the entries of
Departs from it replaces entirely, so that nothing of them is left to compare with the original. A
deviation may have a Tests item between Justification (or Default) and Dropped. It lists the test
files that check the rebuild does what the Reason says; each of them cites the deviation's ID and
runs without the original's files. A complete parity row that a Replaces item names has no tests
of its own, and it is `deviated` once every `mandatory` deviation it lists has a Tests item
(docs/DECISIONS.md, 2026-10-06).

Dated product decisions behind many of these entries, with their full reasoning, are in
[docs/DECISIONS.md](docs/DECISIONS.md).

## DEV-HELP-001

- Departs from: FMT-HELP-001, SCR-UI-009, RULE-HELP-001
- Replaces: RULE-HELP-001
- Reason: The rebuild shows the help topics in its own viewer, opened with F1 or from the menu.
  In the original, Help Topics does nothing and no key opens help: the call that would start the
  Windows help program on the help file is never reached (RULE-HELP-001). The viewer reads the
  player's own help file and approximates the help program's typography.
- Setting: None
- Default: mandatory
- Justification: The original ships a complete help file and a Help Topics item wired to nothing,
  which is logic that plainly does not do what it was written to do. Opening the help adds
  information and changes nothing the player can do or any rule's result. The help program the
  original would have started is no longer part of Windows, so a viewer of the rebuild's own is
  the only way to show the file, and a setting that brings back an inert menu item gives the
  player nothing.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs,
  tests/Rechaos.Tests/ExtractedHelpStoreTests.cs
- Dropped: no

## DEV-HELP-002

- Departs from: FMT-HELP-001, RULE-ATTACK-001
- Reason: The Attack topic gains a labelled note that the attack roll is Combat plus the
  attacker's current Force minus the target's Defense, and that every attack uses the Force the
  gang had at the start of the phase. The shipped help and manual leave out the Force term.
- Setting: None
- Default: mandatory
- Justification: The note is labelled as the rebuild's and changes no text of the shipped help. The
  shipped topic leaves out a term the attack roll uses, and no player gains from not knowing it.
- Dropped: no

## DEV-VIDEO-001

- Departs from: FMT-VIDEO-001, RULE-VIDEO-001
- Reason: The rebuild decodes the two shipped movies with its own decoder for the subset of the
  format they use, where the original calls the Smacker library. A file that is malformed or uses
  anything outside that subset is skipped and play goes on to the next movie or the title screen,
  as a file the original's library fails to open is skipped.
- Setting: None
- Default: mandatory
- Justification: It changes only what happens where the original would fail to play the file, so
  there is nothing after that point to keep or compare.
- Dropped: no

Decided 2026-09-13.

## DEV-VIDEO-002

- Departs from: RULE-VIDEO-001
- Reason: A press of Escape, Enter or Space, or of either mouse button, ends the movie playing,
  and the press is consumed. The original ends a movie only when the left button is held down at
  one of its 100 ms input ticks and ignores the keyboard. While the window is inactive the rebuild
  holds the movie where it is; the original plays on.
- Setting: None
- Default: mandatory
- Justification: This is an interface change that removes friction: a click the original could
  miss between two ticks always takes effect, and the usual keys work. It changes no rule and
  nothing a match starts from, since the movies end before the title screen either way. A setting
  to bring back the missed clicks would give a player nothing.
- Dropped: no

## DEV-VIDEO-003

- Departs from: RULE-VIDEO-001, SCR-UI-001
- Reason: With Intro only once switched on, the rebuild plays the two movies unattended only until
  it has recorded a showing: its preferences file keeps `IntroMoviesSeen`, set once the queue
  drains after at least one movie opened, and every later start goes straight to the title screen.
  The title screen gains an INTRO button that plays the movies again on request. The original plays
  both movies at every start that does not load a saved game.
- Setting: Intro only once
- Default: on
- Justification: Players rarely want to watch the intro again and again; one showing is plenty.
  Playing it at every start makes the player wait through or click past the same two movies each
  time before reaching the title screen, and nothing in a match depends on it. The INTRO button
  plays the movies whenever the player asks, and a player who wants the original's intro at every
  start switches Intro only once off.
- Dropped: no

Made a setting that starts off on 2026-09-25, and switched to start on on 2026-09-26.

## DEV-AUDIO-001

- Departs from: RULE-AUDIO-010
- Replaces: RULE-AUDIO-010
- Reason: The rebuild does not run the original's startup drive check or its unused search of the
  CD drives. It plays the music tracks from the files of the GOG release (`MUSIC/TrackNN.ogg`) and
  never looks for a drive or a disc.
- Setting: None
- Default: mandatory
- Justification: The check always passes and the search is never called, so neither changes any
  game state. Its only effect in the original, the prefix of the movie paths, is replaced by the
  rebuild's own asset paths. A setting would have nothing to switch.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs
- Dropped: no

## DEV-SAVE-001

- Departs from: FMT-SAVE-001, FMT-SAVE-002
- Replaces: FMT-SAVE-001, FMT-SAVE-002
- Reason: The rebuild neither reads nor writes the original's save files. It keeps its own save
  format, with a version number and bounded readers. Each player's selected sector
  (`cursor_sectors`, FND-SAVE-003) goes into the small file the save browser keeps beside each
  save, the autosave and the crash-recovery save, and a load restores it from there. A save whose
  companion file is missing or belongs to another file, and a load that falls back to the backup
  generation, start every player on the sector of its roster slot 0, as a new match starts.
  An online match the client takes up or resumes starts every player on that sector too, since
  the server keeps no selection (DEV-NET-001).
- Setting: None
- Default: mandatory
- Justification: What a player can do in a match is the same whichever format holds it, and the
  rebuild's format adds a version number and bounded readers. A setting would need a reader and
  writer for the original's format, which is a separate scope decision (2026-09-10).
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.Persistence.cs
- Dropped: no

Decided 2026-09-10 ("Save compatibility scope").

## DEV-RNG-001

- Departs from: RULE-RNG-001, RULE-OPTIONS-001
- Reason: The run's sequence is seeded from the low 16 bits of the rebuild's own uptime clock when
  the game object is created, in place of `timeGetTime` at process start. Later local games and
  loads draw on from it, as in the original. Replays, tests and online
  matches take an explicit seed of full width. The rebuild never makes the options loader's two
  `serialNum` draws, so an installation-wide value cannot shift a match's random sequence.
- Setting: None
- Default: mandatory
- Justification: A player cannot tell one random sequence from another, and leaving out the draws
  stops an installation-wide value from shifting a match, so a seed reproduces its match on any
  installation. The original's seed, the time since Windows started, cannot be reproduced anyway. A
  test that replays a fixture from its seed rather than a recorded generator state makes the
  original's six draws itself and cites this entry.
- Dropped: no

## DEV-TURN-001

- Departs from: RULE-TURN-004, RULE-TURN-005
- Reason: The rebuild refuses a recurring Bribe or Snitch, from any source. The original's
  human menus never store one, and its turn-start cleanup would keep one forever if one were
  stored another way.
- Setting: None
- Default: mandatory
- Justification: No input the original accepts reaches the refused case, so no player can tell the
  difference. It closes a path that only the network or a replay could use.
- Dropped: no

## DEV-SETUP-001

- Departs from: RULE-SETUP-001, RULE-SETUP-005, RULE-SETUP-006, RULE-SETUP-007
- Reason: In an online match a modifier name typed by one player would change the match for
  every seat. The online lobby refuses those names, and the client replaces a name whose
  ten-character form is a modifier with the seat's derived name. Hot-seat and single-player
  matches keep the original behaviour.
- Setting: None
- Default: mandatory
- Justification: The original's network play is not reproduced (DEV-NET-001), so there is no
  original online behaviour to keep, and hot-seat and single-player matches are unchanged.
- Dropped: no

The reasoning is in `docs/MULTIPLAYER.md`.

## DEV-SETUP-002

- Departs from: SCR-SETUP-001, SCR-SETUP-002, SCR-AWARDS-001, SCR-OBJECTIVE-002
- Reason: The setup, handoff and endgame screens accept keyboard navigation in addition to the
  original's mouse input.
- Setting: None
- Default: mandatory
- Justification: It adds keyboard input beside the original's mouse input, which works as before.
- Dropped: no

## DEV-HIRE-001

- Departs from: RULE-HIRE-003, SCR-HIRE-002
- Reason: The rebuild refuses a hire drop at once when the sector would hold six of the player's
  gangs, counting gangs already ordered to Move away or Terminate as gone. The original accepts
  the order and fails the hire at resolution.
- Setting: None
- Default: mandatory
- Justification: The player learns at once that the hire cannot happen, where the original lets it
  fail silently at resolution. Every outcome stays reachable: ordering the gang out of the sector
  before dropping the hire lets the hire through, as in the original.
- Dropped: no

## DEV-HIRE-002

- Departs from: SCR-HIRE-002
- Reason: The Hire panel warns when the cash projected at the hire, after the Finance projection
  through the execution phase, would not cover the contract.
- Setting: None
- Default: mandatory
- Justification: It adds a warning and refuses nothing.
- Dropped: no

## DEV-HIRE-003

- Departs from: RULE-HIRE-001
- Reason: The rebuild lets a hire whose cost is 0 through while the player's cash is negative.
  The original was read as failing a hire when its cost is greater than cash, which would refuse
  that hire. The Dropped item corrects that reading.
- Setting: None
- Default: mandatory
- Justification: A hire that costs nothing takes nothing from cash, so refusing it because cash is
  already negative protects nothing and only keeps a player in debt from rebuilding. It adds an
  option and removes none.
- Dropped: 2026-09-25, the original also lets a zero-cost hire through while cash is negative: it
  skips the cash test when the cost is 0 (FND-HIRE-006, RULE-HIRE-001). The earlier reading
  missed the zero-cost branch in front of the cash test.

## DEV-RESEARCH-001

- Departs from: SCR-RESEARCH-001
- Reason: The Research panel shows the research accumulated so far beside each item's total.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. The original shows only
  each item's total, so a player choosing what to fund cannot tell how close an item is to
  completion without writing it down turn by turn. The figure is the progress the game already
  keeps, it is shown only to the player who owns it, and it removes no control and changes no
  rule, so a player loses nothing by seeing it.
- Dropped: no

## DEV-MOVE-001

- Departs from: RULE-MOVE-001, SCR-MOVE-001
- Reason: The rebuild refuses a Move, when it is ordered, into a sector that already holds six
  of the player's gangs. The original's panel has no capacity test (FND-MOVE-007): it accepts the
  order, and RULE-MOVE-002 sends the gang back at resolution.
- Setting: None
- Default: mandatory
- Justification: The player learns at once that the gang cannot enter, where the original accepts
  the order and then sends the gang back at resolution with its turn spent. A gang the player could
  have given a useful order instead is no longer wasted on a Move that cannot happen.
- Dropped: no

The refusal applies to the orders a person gives only. A Move the computer planner plans into a
full sector, for a computer seat or for a human seat a simulation hands to the planner, is planned
as in the original and left to the Move repair, which EXP-TURN-010 depends on.
Whether the original's panel refuses the order too is in `manual_validation_plan.md`. The rebuild
counts only the gangs already in the destination, so moving one gang out and another in to a full
sector takes two turns where the original allows one; counting gangs ordered out of the
destination, as DEV-HIRE-001 does, would remove that difference.

## DEV-MOVE-002

- Departs from: RULE-MOVE-002
- Reason: The rebuild's Move repair counts the times it gives a mover sent back to its own
  sector a random neighbour. After 256 of them in one player's repair, it sends that mover to the
  lowest-numbered sector the player's projected count leaves room in, which need not be a
  neighbour. The original's loop has no bound, and some order sets keep it running for ever
  (FND-MOVE-006).
- Setting: None
- Default: mandatory
- Justification: In the original those orders hang the game in the Move phase, which the Fidelity
  rules allow to be fixed. Below the bound the rebuild follows the original's loop, and a setting
  that brings back the hang gives the player nothing.
- Dropped: no

## DEV-CONTROL-001

- Departs from: RULE-CONTROL-001, BUG-CONTROL-001
- Reason: Only the players who ordered Control in the sector, and its owner, enter the
  comparison, with the original's arithmetic and tie draw among them. The original compares every
  player, so a player with no Control order there has the margin -(Income + Support) and is handed
  the sector, or drawn with the real challenger, when that sum is negative. The shipped site table
  has sites with negative Support, so the case can arise.
- Setting: None
- Default: mandatory
- Justification: The original's behaviour is a bug (BUG-CONTROL-001). The pass gives every player
  slot a pool, and a player with no order there starts at 0, so when the sector's Income plus
  Support is negative that player's margin comes out positive. The player is then handed a sector
  they never tried to take, or ties with and can beat a challenger who would otherwise win alone.
  Nothing about it reads as design: the manual describes the comparison only among players who
  try to control the sector, the case needs completed sites with negative Support and no police,
  the player gets no cue that it can happen, and no player source relies on it. Restoring it
  would only hand sectors to bystanders by accident, so it gets no setting. A player who wants
  the sector can still order Control.
- Dropped: no

The fix changes which player owns the sector when the case arises. Decided as mandatory on
2026-09-26.

## DEV-CONTROL-002

- Departs from: RULE-CONTROL-001
- Reason: When police are present in a sector as the Control pass runs, the original leaves the
  sector out of Control without a word. The rebuild records a failed Control result for each
  gang ordered to Control it. The owner does not change in either case.
- Setting: None
- Default: mandatory
- Justification: It adds a report of what happened. The owner of the sector is the same in both.
- Dropped: no

## DEV-GANG-001

- Departs from: SCR-GANG-002
- Reason: Hovering one of the fourteen statistics of a live gang or a hire offer shows its base value and one signed
  line for each item and site that changes it.
- Setting: None
- Default: mandatory
- Justification: It adds information on hover and changes nothing else.
- Dropped: no

## DEV-EQUIP-001

- Departs from: RULE-EQUIP-001, RULE-EQUIP-002, RULE-SELL-001
- Reason: The original carries out Equip and Sell in a scan by player slot and roster slot, so a
  Sell in an earlier slot can pay for an Equip in a later one whatever order the player gave them
  in. The rebuild debits and credits cash in the order the player last submitted Equip and Sell
  orders, a replaced order moving to the end. Players still resolve by slot, Give keeps its
  deferred deliveries in roster order, and a buyer whose cash exactly equals the price still buys.
- Setting: None
- Default: mandatory
- Justification: The roster-slot order has no meaning in play. The player never sees a gang's
  roster slot while giving orders, so whether a Sell pays for an Equip is decided by a number the
  player cannot read, and an order that looks affordable fails for no visible reason. The rebuild
  resolves Equip and Sell in the order the player scheduled them, which the player controls and
  the cash row of the console shows, so this is strictly better and needs no setting to restore
  the original. Every outcome of the original stays reachable, since giving Sell and Equip in slot
  order reproduces the original's scan.
- Dropped: no

Kept mandatory on 2026-09-26, after a proposal to put it behind a setting that starts off.

## DEV-EQUIP-002

- Departs from: SCR-EQUIP-001
- Reason: The purchase panel draws the gang's held items in three 20-by-20 boxes under the
  portrait, and a double-click on the portrait opens the gang information panel and returns to
  the same selection.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. Buying an item replaces
  and destroys the one the gang holds in that slot, and the boxes keep the held items in sight
  while the player chooses, so a better item is not thrown away by accident. They show the gang's
  own items, which the gang information panel already shows, and the double-click only opens that
  panel and comes back. Every original control works as before and no rule changes.
- Dropped: 2026-09-25, the original draws the held items and opens the gang panel on a portrait double-click (FND-EQUIP-010).

## DEV-GIVE-001

- Departs from: SCR-GIVE-001
- Reason: Up and Down cycle the Give recipient.
- Setting: None
- Default: mandatory
- Justification: It adds keys, and the mouse works as before.
- Dropped: no

## DEV-FINANCE-001

- Departs from: SCR-FINANCE-001, RULE-FINANCE-001
- Reason: The Equipment row credits a Sell order with half the Cost of the one item the resolver
  pays for (RULE-SELL-001, BUG-SELL-001). The original adds half the Cost of every item the order
  selects (FND-FINANCE-002). The other seven rows follow FND-FINANCE-002.
- Setting: None
- Default: mandatory
- Justification: The panel is a forecast of next turn's cash, and on a multi-item Sell the
  original's forecast names cash the resolver never pays. Showing the amount that will arrive adds
  information and changes no order or result. A setting would only bring back a figure known to be
  wrong.
- Dropped: no

## DEV-ATTACK-001

- Departs from: SCR-ATTACK-001
- Reason: A double-click on a target cell, without moving, opens the enemy gang's information
  panel and returns to the picker.
- Setting: None
- Default: mandatory
- Justification: It adds a shortcut to a panel the player can already open.
- Dropped: 2026-09-25, the original opens the same panels on a double-click (FND-ATTACK-004).

## DEV-ATTACK-002

- Departs from: RULE-ATTACK-002
- Reason: The rebuild also refuses an Attack on a gang the player has not detected when the order
  is submitted, so an order from the network or a replay cannot target one. The original only
  offers detected gangs in the picker.
- Setting: None
- Default: mandatory
- Justification: No input the original accepts reaches the refused case, so no player can tell the
  difference. It closes a path that only the network or a replay could use.
- Dropped: no

## DEV-COMBAT-001

- Departs from: BUG-COMBAT-001, SCR-COMBAT-002
- Reason: The original is reported to freeze while it presents Detailed Combat. The rebuild's
  presentation always ends, Escape, Cancel or a right-button press clears the queue of clips, and
  playback never changes match state.
- Setting: None
- Default: mandatory
- Justification: The original is reported to stop responding here, so nothing after that point can
  be played or compared. Playback never changes match state.
- Dropped: no

## DEV-COMBAT-002

- Departs from: SCR-COMBAT-001, RULE-COMBAT-004
- Reason: The Combat Summary panel has a Detail control, also on the D key, that replays the
  selected fight, and the Detailed Combat option governs only the automatic presentation. The
  original's Combat Results panel has no control that opens Detailed Combat.
- Setting: None
- Default: mandatory
- Justification: It adds a control, and the Detailed Combat option still decides the automatic
  presentation as in the original.
- Dropped: no

## DEV-AI-001

- Departs from: BUG-AI-001, RULE-AI-010
- Reason: In the guards against a second family-6 hire, the rebuild compares the previous hire
  role with role 4 in every scenario. The original compares it with the scenario's schedule slot
  number (6, 5, 2 or 10), which never matches a family-6 hire and in Dominance never matches
  anything. Schedule tables, quotas, ranking and draws are unchanged.
- Setting: None
- Default: mandatory
- Justification: The guard is written to stop a computer player hiring a second hunter straight
  after the first, and compares with a slot number that never matches, so in Dominance it never
  fires and elsewhere it fires after unrelated hires. Correcting the comparison makes the computer
  players follow their own schedule; no player strategy that depends on the original comparison is
  recorded.
- Dropped: 2026-09-26, the correction made no difference that could justify it. In 2,688 headless
  four-year matches of the rebuild, in the seven scenarios with a hunter guard, at every
  Mentality and under both AI policies, with a simulated human in seat 0, the original comparison
  and the corrected one produced identical matches: the hunter force reached its guard only in
  Armageddon, and the scenario's later adjustments overwrote every slot the two comparisons
  disagreed on. With no measured gain, the Fidelity rules keep the original, and the rebuild
  compares with the hunter slot number again (BUG-AI-001, RULE-AI-010). The measurements are in
  the 2026-09-26 entry "Keep the original hunter guard and drop DEV-AI-001" of docs/DECISIONS.md.

The correction applied under the Original AI policy as well. Decided 2026-09-17.

## DEV-AI-002

- Departs from: RULE-AI-002, RULE-AI-019, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-023, RULE-AI-024, RULE-AI-025, RULE-AI-026, RULE-AI-027, RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031, RULE-MOVE-002, RULE-EQUIP-001, RULE-EVENT-014, RULE-INFLUENCE-001, RULE-SITE-001
- Reason: A computer player's planned action becomes a command only when a human could give the
  same order and the planner's running total of this turn's costs leaves cash for it; any other
  planned action is kept in the planning state and gives the gang no command. The original stores
  every planned action in the gang record and resolves it: a Move to the gang's own sector or
  into a full sector goes to the six-gang repair (RULE-MOVE-002), an Equip the player can no
  longer pay for is refused when it resolves (RULE-EQUIP-001) and leaves the player a cash report
  (RULE-EVENT-014), and an Influence in a sector the player does not control rolls with no owner
  test (RULE-INFLUENCE-001, EXP-TURN-083), and a site it completes in a neutral sector counts in
  the sector record (RULE-SITE-001). In 21 computer-only
  matches of 26 turns those three kinds came to 478 orders, and no other planned action a human
  could not order was seen. A fourth kind follows from `local_tech_cap` reading the research
  level of a sector whoever owns it (RULE-AI-026): a Research above the Tech limit the Research
  list allows there, which the original resolves because RULE-RESEARCH-001 tests no Tech Level.
  The gang's planning history is the same; its resolved action can differ.
- Setting: None
- Default: mandatory
- Justification: A computer player's gang is held to the same legal orders as a human's, so it
  cannot carry out an action no player could order. When the dropped action is a Move to the
  gang's own sector the gang stays where it is either way, and in every case its planning history,
  which later turns read, is kept.
- Dropped: no

The resolved action can differ from the original's, which changes the match when it does. Decided
2026-09-17.

## DEV-AI-003

- Departs from: RULE-AI-001, RULE-AI-002, RULE-AI-019, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-023, RULE-AI-024, RULE-AI-025, RULE-AI-026, RULE-AI-027, RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031, RULE-UI-009, SCR-UI-008
- Reason: A second computer-player policy keeps every command the original planner chooses and
  gives an idle gang at most one legal, affordable fallback of its own scoring, examining gangs in
  ascending order. Resolution odds are unchanged. With Advanced AI on, Game Information names the
  policy after the Mentality text.
- Setting: Advanced AI (Original is the original planner)
- Default: off
- Dropped: no

The policy is chosen for a new match and kept by it; loaded saves keep the policy they were
started with.

## DEV-AI-004

- Departs from: RULE-AI-013
- Reason: A hire placement anchor of 164 (sector 100) always fails the rebuild's keep test, so the
  fixed scans replace it at the next refresh. The original's keep test reads the owner byte of
  sector 100, which lies past the end of the sector list, and keeps the anchor or not by whatever
  that byte holds.
- Setting: None
- Default: mandatory
- Justification: Anchor 164 is stored only for a player whose Right Hands slot is empty when the
  match is set up, which the original's setup never produces, so no match the original can play
  reaches the read. What the stray byte holds depends on memory outside the sector list that the
  rebuild does not lay out, and keeping an anchor that names no sector is logic that plainly does
  not do what it was written to do; a setting would choose between a rescan and a guess.
- Dropped: no

## DEV-AI-005

- Departs from: RULE-AI-006
- Reason: A routing step of the computer players' sector selector that would leave the city is not
  taken, and the gang stays in the row or column it is in. The original takes the step whenever
  the gang count it reads for the new sector number is 5 or less, and that number then lies
  outside the player's row of `sector_gang_count`: in another player's row, in the constants
  before the list or in the selector's own pairs after it (FND-AI-066).
- Setting: None
- Default: mandatory
- Justification: Only a target read from past the end of the selector's list can lie off the
  board, and the step then stores a sector number outside the city as the gang's destination. The
  Move phase sets the gang's sector to that number and the capacity repair counts it in a 64-entry
  list (RULE-MOVE-001, RULE-MOVE-002), so the original writes outside that list and leaves a gang
  on no sector: corrupted state that no player can rely on. No recorded run reaches such a step,
  and a setting would choose between refusing the step and reproducing the corruption.
- Dropped: no

## DEV-AI-006

- Departs from: RULE-AI-006
- Reason: When the sector selector's common block tests a sector of column 7 with a row of 1 or
  more and the sector passes, the original multiplies a dword past its score table by five: one
  of the first seven dwords of player 0's planning records, which hold the family, action, target
  and cooldown bytes of records 0 and 1 (FND-AI-069). The rebuild leaves the records unchanged.
- Setting: None
- Default: mandatory
- Justification: The write is an index past the end of a 64-entry table, and it rewrites another
  player's planning state with bytes that are no family, action or target any handler assigns: a
  corruption no player can rely on. While a human holds slot 0 the records are zero bytes and the
  multiply leaves them zero, so the usual match plays the same. A setting would choose between
  leaving the records alone and reproducing a corruption the rebuild's planning state refuses to
  hold.
- Dropped: no

## DEV-AI-007

- Departs from: RULE-AI-006, RULE-MOVE-001
- Reason: A computer player's gang moves only to a sector next to its own, as a human's does. The
  original's sector selector can return a sector several steps away: when the first of its sorted
  pairs is a neighbour it returns the tie-break's pick, and a pair that the family-0 and family-1
  filter kept from an earlier call can tie with the neighbours (RULE-AI-006). The Move pass then
  puts the gang in that sector at once (RULE-MOVE-001, EXP-TURN-015). The rebuild refuses such a
  Move when the planned action becomes a command, as DEV-AI-002 does with the other planned
  actions a human could not order, so the gang has no order that turn and keeps its planning
  history.
- Setting: `--original-computer-moves` on the game's command line, inverted (the flag switches the
  deviation off for the local matches started in that session). No screen offers it, and an
  online match keeps the deviation on.
- Default: on
- Justification: The jump comes from a pair left over from another gang's search, which the
  selector was not written to return, and it lets a computer player's gang do what no human's can:
  the same rules apply to every player. It costs no measurable balance. Such Moves come to about
  25 a match in Kill 'Em All and 12 in Power and Big 40. In 973 pairs of matches of every scenario,
  played to turn 208 from the same seeds with a planner-played human seat that could not jump, the
  human seat survived to the end in 45% of the matches when the computer players could jump and in
  47% when they could not, a difference within the matches' noise (95% interval of 2.8 points
  either way), and its turns survived and sectors held did not change beyond noise either. Since
  no player can notice the difference, the setting stays off the Options screen; the flag serves
  the replays of recorded runs, which run with it switched off, and a player who wants the
  original's Moves.
- Dropped: no

## DEV-AI-008

- Departs from: RULE-AI-012, RULE-HIRE-001
- Reason: A computer player's hire goes only to a sector the player controls or holds a gang in, as
  a human's does. The original's hire resolver has no such test, so a hire the computer planner
  places anywhere else is carried out: the new gang's Force is rolled and the gang appears there
  (RULE-HIRE-001, EXP-TURN-090). The rebuild drops such a hire when the planner makes it, so the
  player hires nothing that turn and the offer stays in its pool.
- Setting: `--original-computer-hires` on the game's command line, inverted (the flag switches the
  deviation off for the local matches started in that session). No screen offers it, and an
  online match keeps the deviation on.
- Default: on
- Justification: The same rules apply to every player, and the difference costs no measurable
  balance. The original tests the owner where each hire is placed instead of in the resolver:
  the hire panel accepts a drop only on a sector the player owns or holds a living gang in
  (SCR-HIRE-002), and the two random modes of the computer's own destination helper choose only
  among such sectors (RULE-AI-012). The planner's encoded sector passes neither place, so the
  hire EXP-TURN-090 reaches falls through a gap between them. No recorded or simulated match
  reaches it without the probe's written families, so no player meets it or can build a strategy
  on it. In 500 pairs of four-year matches, 50 seeds of every scenario played from the same
  seeds with a planner-played human seat that keeps the human hire rule, no computer hire went to
  such a sector, and every pair ended the same way with the deviation on and off: computer players
  won 164 of the 438 matches that ended, and the human seat survived in 173. The other 62 pairs,
  untimed matches still running at the simulation's turn limit, stopped at the same turn with the
  same events either way. EXP-TURN-090 reaches such a hire only after the probe writes family 5
  into every computer gang. Since no player can notice the difference, the setting stays off the
  Options screen; the flag serves the replays of recorded runs, which run with it switched off,
  and a player who wants the original's hires.
- Dropped: no

## DEV-EVENT-001

- Departs from: SCR-EVENT-001
- Reason: With Event Site Images set to Smooth, the stretched site picture of the Last Turn
  Events panel is filtered linearly and drawn without the ordered mask.
- Setting: Event Site Images (Smooth; Original is the original's drawing)
- Default: off
- Dropped: no

## DEV-EVENT-002

- Departs from: SCR-EVENT-001
- Reason: The rebuild records which report pages the player has visited and keeps the Events
  indicator blinking until every page has been seen.
- Setting: None
- Default: mandatory
- Justification: It keeps a reminder on until the player has read every page. Nothing in the match
  depends on it.
- Dropped: no

The original's mechanism for the blinking light has no finding yet, so this may turn out to match
it.

## DEV-COMLINK-001

- Departs from: SCR-COMLINK-001
- Reason: Escape and the right mouse button close Comlink View, as they close the rebuild's other
  panels.
- Setting: None
- Default: mandatory
- Justification: It adds ways to close the panel; the original's still work.
- Dropped: no

## DEV-SEARCH-001

- Departs from: SCR-SEARCH-001
- Reason: The keyboard moves between Search rows and toggles them. The original's handler reacts
  only to Enter and Execute. A gold border marks the keyboard's row once an arrow key has moved
  it or Space has flipped it; until then the panel draws what the original draws.
- Setting: None
- Default: mandatory
- Justification: It adds keys, and Enter and Execute work as before.
- Dropped: no

## DEV-UI-001

- Departs from: RULE-UI-003, SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008, SCR-OPTIONS-001,
  SCR-GANG-001, SCR-GANG-002, SCR-FINANCE-001
- Reason: With Slide Panels on, a panel slides in as in the original but closes at once. The
  original's closing slide holds input for about a quarter of a second, and without it the close
  cue and the next cue start in the same frame, so the next cue cuts the close cue off.
- Setting: None
- Default: mandatory
- Justification: The closing slide holds input for about a quarter of a second and makes the next
  sound cue cut off the close cue. A panel's closing animation has no effect on play, and the
  opening slide is kept for a player who wants the motion.
- Dropped: no

## DEV-UI-002

- Departs from: RULE-UI-012, SCR-UI-004
- Reason: The detailed-sector screen's 3-by-3 minimap also draws the Siege and Big Man pylons,
  scaled, so the player can see neighbouring objective sectors. The original draws them only on
  the city map.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. The Siege and Big Man
  sectors are already marked on the city map, so the minimap tells the player nothing new; it
  saves leaving the sector view to find out whether a neighbour is an objective. It is drawn only
  from what the city map shows, removes no control and changes no rule.
- Dropped: 2026-09-27. FND-UI-018 shows that the original's nine-sector display is a crop
  of the prepared city map, which already carries the pylons, so the rebuild draws them there
  as the original does.

## DEV-UI-003

- Departs from: SCR-UI-004, SCR-MOVE-001, RULE-TURN-005
- Reason: A ctrl-click picks several gang cards, and Attack, Control, Heal, Hide, Influence or
  Move is then given to all of them at once. Each gang is validated on its own, and a bulk Move
  counts the whole selection against the destination's room before queueing. The original's group
  order strip goes through the same checks, so a gang that could not take the order on its own
  keeps its previous one where the original would write the order anyway.
- Setting: None
- Default: mandatory
- Justification: Each gang receives the order the player could give it on its own, validated on its
  own, so the selection only saves clicks.
- Dropped: no

## DEV-UI-004

- Departs from: SCR-UI-001
- Reason: After one complete unattended run of the logo and intro movies, the rebuild opens at
  the title screen, which gains a button that plays them again.
- Setting: None
- Default: mandatory
- Justification: The movies stay available from the title screen, and a player who has already
  watched them is not made to sit through them again.
- Dropped: 2026-09-26, DEV-VIDEO-003 covers the same behaviour and the INTRO button as a setting
  that starts on (the 2026-09-25 and 2026-09-26 decisions in `docs/DECISIONS.md`).

## DEV-UI-005

- Departs from: SCR-UI-003, SCR-UI-004, SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008
- Reason: Hover tooltips explain statistics, attributes, modifiers, modes, options and ranking
  scores, and a two-second rest on a command explains the order.
- Setting: None
- Default: mandatory
- Justification: It adds information on hover and changes nothing else.
- Dropped: no

## DEV-UI-006

- Departs from: SCR-UI-003, SCR-UI-004, SCR-FINANCE-001
- Reason: The city console shows next turn's projected cash beside the current Cash, as
  `CASH 20 [18] (+1)`: cash, the cash left after queued Bribe and Equip prices, and the change
  over the whole cycle, with a breakdown on hover.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. Every figure is one the
  player could work out from the Finance panel and the orders already queued, and the original makes
  the player do that sum by hand before each purchase. Showing it at a glance helps the player avoid
  Equips that fail for lack of cash, and it is the display that makes the order of purchases
  (DEV-EQUIP-001) readable. It removes no control and changes no rule.
- Dropped: no

## DEV-UI-007

- Departs from: SCR-UI-003, SCR-UI-004, RULE-UI-011
- Reason: Hovering the Tolerance value shows the range the player's queued Chaos can reach, and
  the value turns orange when that range can set off a Crackdown.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. A Crackdown follows from
  Tolerance and the Chaos the player queued, both known to the player, but the original leaves the
  player to work out the range by hand, and a miscalculation sets off a Crackdown the player did not
  intend. The warning only states that result in advance, removes no control and changes no rule;
  the player can still order the Chaos.
- Dropped: no

## DEV-UI-008

- Departs from: SCR-UI-004, SCR-ATTACK-001, SCR-MOVE-001
- Reason: The command pickers name valid targets, and hovering a gang with a queued Move,
  Influence or Attack highlights its target on the board.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. In the original a queued
  order's target is not drawn on the board, so checking a turn's plan means recalling or reopening
  each gang's order. The pickers and highlights show only orders the player has given and targets
  the player may choose, remove no control and change no rule.
- Dropped: no

## DEV-UI-009

- Departs from: SCR-UI-006
- Reason: A double-click on an equipped item in the gang information panel opens Item Information
  and returns to the same gang.
- Setting: None
- Default: mandatory
- Justification: It adds a shortcut to a panel the player can already open.
- Dropped: no

## DEV-UI-010

- Departs from: SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008, SCR-OPTIONS-001, SCR-GANG-001,
  SCR-GANG-002, SCR-FINANCE-001, SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001, SCR-MOVE-001,
  SCR-RESEARCH-001, SCR-INFLUENCE-001, SCR-HIRE-001, SCR-OBJECTIVE-001, SCR-SEARCH-001,
  SCR-EVENT-001, SCR-COMLINK-002, SCR-UI-003, SCR-UI-004, SCR-HIRE-002, SCR-SETUP-001
- Reason: Panels accept keyboard navigation, and Escape, Backspace and the right mouse button
  cancel them; the arrow keys cycle gangs on the gang information panel, pick a cell on
  the Move panel and a row on the Equip panel, and the keys 1 to 3 toggle items on the Give and
  Sell panels. A right press also lets go of a held console tile, Hire offer or reject cross, or
  setup portrait without acting on it, and backs out of the setup screen to the title.
  The original's panels take Enter and Execute and, on the idle-gang warning, Escape, and every
  screen but the About credits, the console and the detailed sector screen ignores the right
  button (FND-UI-063).
- Setting: None
- Default: mandatory
- Justification: It adds keys and ways to cancel; the original's Enter and Execute work as before,
  and where the original gives the right button a meaning (the console tiles and the detailed
  sector screen) the rebuild keeps it. A right press that does nothing is not something a player
  plans around, so the original's behaviour is not worth a setting.
- Dropped: no

## DEV-UI-011

- Departs from: SCR-UI-009
- Reason: Saving and loading use nine named slots, and Escape opens a pause menu. The original
  saves and loads from its menu bar.
- Setting: None
- Default: mandatory
- Justification: Saving and loading stay available wherever the original allows them, and slots with
  names replace a file dialog that the original's menu bar opens.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs, tests/Rechaos.Tests/GameMenuLayoutTests.cs,
  tests/Rechaos.Tests/SaveSlotCatalogTests.cs
- Dropped: no

## DEV-UI-012

- Departs from: SCR-UI-001
- Reason: The title screen shows the build version and a Report Bug control.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. A bug report is useful only
  when it names the build it came from, and the original gives the player no way to report a
  problem from inside the game. The label and the control sit on the title screen, before any
  match, so they touch no rule and nothing a match starts from, and every original control works
  as before.
- Dropped: no

## DEV-UI-013

- Departs from: SCR-UI-004, SCR-GANG-002
- Reason: The detailed-sector screen's portrait strip marks each opponent with detected gangs in
  the sector, and the player can page that opponent's detected gangs on the cards and open their
  gang information panels. The original
  lists only the viewer's own gangs.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. Only gangs the player has
  already detected are shown, so what the player can know is unchanged; the original makes the
  player leave the sector view to look them up before ordering an Attack or a Move. It removes no
  control and changes no rule.
- Dropped: 2026-09-27. FND-UI-015 and FND-UI-018 show that the original does this: a press on
  a portrait of the Overlord bar shows that player's gangs in the sector that the active player
  can see, the portrait of a player with none is drawn from the dimmed row, and a double-click on
  another player's card opens the gang or item panels. The rebuild does the same.

Decided 2026-09-18.

## DEV-UI-014

- Departs from: SCR-UI-004
- Reason: The detailed-sector screen shows how many police turns remain in the sector.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. Control does not settle a
  sector while police are there (RULE-CONTROL-001), so how long they stay decides when a Control
  order there can succeed; without the count the player has to track the turns by hand. The count
  is the value the game already keeps for the sector. It removes no control and changes no rule.
- Dropped: no

Whether the original shows the count is not recorded.

## DEV-UI-015

- Departs from: RULE-UI-013
- Reason: A second copy of the rebuild starts and runs beside the first. The original refuses a
  second copy and brings the running one to the front.
- Setting: None
- Default: mandatory
- Justification: Refusing a second copy only takes a choice from the player. Nothing in a match
  depends on there being one copy: the copies share only the rolling autosave, which a file guard
  keeps one writer at a time, and each keeps its own match. A second copy is also how one computer
  holds two seats of an online match. A setting that restored the refusal would offer nothing
  but the loss of that choice.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.Persistence.cs,
  tests/Rechaos.Tests/RollingAutoSaveTests.cs
- Dropped: no

## DEV-UI-016

- Departs from: RULE-UI-013, RULE-UI-014, SCR-UI-009, FMT-DATA-004
- Replaces: FMT-DATA-004
- Reason: Only the 16-bit image set is drawn, and there is no Thousands of Colors option. The
  original draws the same set at any display deeper than 8 bits, and loads the 256-colour palette
  of `DATA/CLT00002` and the 8-bit set only on an 8-bit display, so the game never reads either.
  The extractor still copies both into the asset pack, and decodes the 8-bit set there, where
  nothing loads them.
- Setting: None
- Default: mandatory
- Justification: The original defaults to the 16-bit set. The 8-bit set holds the same pictures
  reduced for 256-colour displays, which no current display is, so a setting would switch to a
  poorer copy of the same pictures.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs
- Dropped: no

## DEV-UI-017

- Departs from: RULE-UI-014
- Reason: Closing the window ends the program after the rolling autosave is written. The original
  treats a close as File, Exit and offers to save during a game.
- Setting: None
- Default: mandatory
- Justification: The rolling autosave keeps the match as it stood at the start of the turn, and
  orders given since then can be saved from the Escape menu before closing. The rebuild's saves go
  to named slots, so the original's save dialog has no counterpart to offer.
- Dropped: 2026-10-06, the rebuild now asks as the original does (RULE-UI-015): closing the window
  or quitting to the title while the match changed since it was last saved or loaded offers to save
  first, cancel, or leave without saving. Asking only when something would be lost costs a player
  nothing, and the autosave holds only the start of the turn. The autosave is still written on the
  way out.

## DEV-UI-018

- Departs from: RULE-UI-014, FMT-STATE-009
- Replaces: FMT-STATE-009
- Reason: Keyboard and mouse state is read once per frame, 60 times a second, and each screen acts
  on what changed since the last frame. There is no event queue, accelerator table or menu command
  event; the options are on the Options screen. A double-click is two presses on the same target
  within 500 ms. Online text fields take characters from the platform keyboard layout.
- Setting: None
- Default: mandatory
- Justification: It changes how commands are reached and leaves what they do alone. Every option
  and command the original's event step handles stays reachable.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs
- Dropped: no

## DEV-UI-019

- Departs from: SCR-UI-009, SCR-UI-002, SCR-UI-001, RULE-UI-013
- Replaces: SCR-UI-009
- Reason: The rebuild has no menu bar. Its commands are reached elsewhere: saving, loading and
  quitting from the Escape menu (DEV-UI-011), the options from the Options screen, Help Topics
  with F1 (DEV-HELP-001), full screen with F11 (DEV-OPTIONS-003), and About, which shows the
  credits screen, with Shift+F1. On the title screen, buttons for New Game, Load, Online,
  Options, Help, Intro and Quit stand in for the menu, drawn over the title art with the rebuild's
  name, its credit line and a box for notices left by the previous screen. Ctrl+H and Ctrl+J open the
  Online screen, where hosting and joining happen, and Enter and F9 also start a new game and
  open a saved one. A left press on the title outside the buttons does nothing, where the
  original's title loop takes a press anywhere as New Game.
- Setting: None
- Default: mandatory
- Justification: Every command of the menu bar stays reachable, and the drawing area is drawn
  without the Windows frame above it (DEV-GFX-001), where a menu bar would have no place. The
  title buttons are the only way to Online and Options before a match, and the clicks that skip
  the intro movies land on the title; if a press anywhere started a new game, those clicks would
  carry a player past the menu into setup. New Game stays one button or one key away.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs, tests/Rechaos.Tests/GameMenuLayoutTests.cs,
  tests/Rechaos.Tests/ProgramShellParityTests.cs
- Dropped: no

## DEV-UI-020

- Departs from: SCR-UI-003
- Reason: The city screen takes single keys the original does not: W, A, S and D move the selected
  sector as the arrows do; C opens the orders of the selected gang, G selects the next gang, I
  opens the detailed sector screen, F the City Financial panel, R Player Ranking, T the item list,
  B Combat Results, X Search, H the Hire panel, M Comlink View, N Comlink Send and J Game
  Information; Space ends the player's planning; and in a local match F5 and F9 open the save
  browser to save or load, and F6 and F10 save and load a replay.
- Setting: None
- Default: mandatory
- Justification: Each key reaches a panel or command the console already offers, so it adds
  keyboard input beside the original's mouse input and changes no order or result. The original's
  keys, the arrows and Enter, work as they do in the original, and a setting that took the extra
  keys away would give the player nothing.
- Dropped: no

## DEV-UI-021

- Departs from: SCR-UI-004
- Reason: The order menus of a gang card and of the group order strip are a panel the rebuild
  draws at (248,50,174,400), listing the same orders, where the original opens a Windows popup
  menu at the card's corner or at (290,65). An order the rules refuse for the gang is refused when
  it is chosen, with a message on the console. The panel opens and closes without sliding and
  without a sound, as the popup does (FND-UI-057), and the order panel chosen from it slides in
  with the panel-open sound (RULE-UI-003).
- Setting: None
- Default: mandatory
- Justification: The rebuild draws the whole game in its own window without the Windows frame
  (DEV-GFX-001), where a native popup menu would appear outside the game's picture and ignore its
  scaling. The panel offers the orders the original's menus offer, for the same gangs, so the
  player can do nothing new, and there is no original form to switch back to.
- Dropped: no

## DEV-UI-022

- Departs from: SCR-UI-004
- Reason: The original's drag of a gang card's portrait (FND-UI-044) gives a one-off Move to an
  enabled neighbour of the nine-sector display or a recurring Influence of an unfinished site of
  an owned sector (FND-TURN-009). The rebuild's drag also gives an Attack when dropped on an enemy
  gang's card and a recurring Control when dropped on the gang's own sector, highlights the
  sectors the gang may move to while it is dragged, and drops the drag without an order on Escape
  or a right press. The hold itself, from the press until the left button comes up, and the
  distance the pointer moves before the drag starts are the original's.
- Setting: None
- Default: mandatory
- Justification: Each extra drop gives an order the card's menu already gives, validated as the
  menu's order would be, so the drag changes how an order is entered and never which orders are
  possible or what they do; the original's Move and Influence drops are kept. A setting could only
  take the extra drop targets, the highlight and the cancel away, which no rule or strategy
  depends on. The original's drop rules have not yet been compared case by case with the
  rebuild's.
- Dropped: no

## DEV-UI-023

- Departs from: SCR-UI-003, SCR-UI-004
- Reason: A line at `(438,354)`, between the console and the Hire dock, says in words why an
  order, a drop or a key was refused. A line along the bottom of the city map lists the rebuild's
  keys, and in an online match says where the turn stands instead.
- Setting: None
- Default: mandatory
- Justification: Both lines add information and change nothing the player can do. The original
  answers a refused order with the reject sound alone, which the rebuild still plays; the line
  says which rule refused it. The key line names keys DEV-UI-020 adds, which the original's
  screens cannot show, and online it replaces them with the turn's state, which a player waiting
  on others needs. A setting that hid them would only take information away.
- Dropped: no

## DEV-UI-025

- Departs from: RULE-UI-004
- Reason: Where a number cell's source column lies wholly or partly outside the glyph sheet's
  bitmap, the original changes only the pixels whose source lies inside and leaves the rest of
  the cell holding whatever an earlier draw left on the screen, a glyph of an earlier value
  included (EXP-UI-002). The rebuild draws the same inside pixels, and the rest of the cell shows
  what the rebuild drew beneath it in the same frame: the panel or console background.
- Setting: None
- Default: mandatory
- Justification: The rebuild draws every frame afresh from the match state and keeps no screen
  between frames, so there is no earlier draw for the cell to keep. Only a value whose first
  quotient is 69 or more, or -2147483648, reaches it, and what the original then shows depends on
  the order in which earlier values were drawn, which no rule or strategy uses. Where nothing was drawn before,
  as at a panel's first draw, the two agree pixel for pixel (EXP-UI-027, EXP-UI-028).
- Tests: tests/Rechaos.Tests/ItemInformationLayoutTests.cs
- Dropped: no

## DEV-UI-027

- Departs from: SCR-UI-003, SCR-UI-004, SCR-UI-006, SCR-GANG-002, SCR-SELL-001, SCR-GIVE-001, SCR-EVENT-001, SCR-OPTIONS-001, SCR-COMLINK-002
- Reason: With Steady Lights on, nothing on the match screens blinks or cycles while the player
  does nothing: the Events, Comlink and Done lights stay lit for as long as they would blink, the
  selected sector's frame and the Overlord bar's marker and empty-seat art hold their first
  frames, the idle gang warning's line stays drawn, the Comlink Send caret stays inverse, and the
  rotating item pictures hold their first frame. Off, they blink and turn as in the original.
- Setting: Steady Lights (On holds them still; Off is the original's drawing)
- Default: off
- Tests: tests/Rechaos.Tests/SteadyLightsTests.cs
- Dropped: no

## DEV-GFX-001

- Departs from: RULE-GFX-002, RULE-UI-013, RULE-UI-014
- Replaces: RULE-GFX-002
- Reason: The 640-by-460 drawing area is drawn into a resizable window. The window opens at the
  largest whole multiple of the area, up to 2, that fits in nine tenths of the display; a resized
  window draws the area at the largest scale that fits, whole or not, and letterboxes it. Full
  screen is a borderless window at the desktop's mode in 32-bit colour, with no menu bar above the
  area, and it stays open when it loses focus. The original sizes a window under the Windows menu
  bar, or switches the display to 640 by 480 at 8 or 16 bits and minimizes itself when it loses
  focus.
- Setting: None
- Default: mandatory
- Justification: Every pixel of the drawing area is the original's. The window opens at a whole
  multiple, which keeps each pixel square, and only a window the player resizes scales by a
  fraction. At one to one the area is a small patch on a current display, and many current
  drivers no longer offer 640 by 480 at 8 or 16 bits, so a mode-switch setting would offer a mode
  the display may refuse. No rule depends on the window.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs
- Dropped: no

## DEV-GFX-002

- Departs from: FMT-GFX-001, SCR-EVENT-001
- Reason: PX06008 supplies 157 valid rows, but the original uploads 158 rows
  (FND-GFX-005), reading the top row from outside its allocated pixel buffer.
  The rebuild draws that top row black and positions the 157 valid rows below
  it without stretching. Its 242nd column remains the original black padding.
- Setting: None
- Default: mandatory
- Justification: Reading unrelated memory is unsafe and produces undefined
  artwork that no player can rely on. A setting cannot reproduce the original
  process's unrecorded memory. A black row preserves the valid pixels and their
  placement without inventing additional illustration content.
- Dropped: no

## DEV-TIMER-001

- Departs from: RULE-TIMER-004, RULE-UI-008, RULE-UI-003
- Reason: The panel slide takes its step from a fixed benchmark of 84 copies a second where the
  original measures the machine for one second at startup. Presentation ticks are counted from the
  game clock, so a tick that falls during a long frame is counted rather than lost. The ticks a
  pointer hold or a soundtrack fade keeps from the event pump, or a hold keeps from a panel's own
  loop, are dropped as in the original (FND-UI-046, FND-UI-047, FND-AUDIO-017).
- Setting: None
- Default: mandatory
- Justification: The original's slide speed depends on the machine it runs on, which AGENTS.md
  lets the rebuild fix; 84 copies a second gives the original's 16-pixel step. Apart from a
  pointer hold and a soundtrack fade, which the rebuild reproduces, a tick is lost in the original
  only when the machine stalls, and no rule reads the ticks.
- Dropped: no

## DEV-OPTIONS-001

- Departs from: RULE-OPTIONS-001, RULE-OPTIONS-002, BUG-OPTIONS-001, BUG-OPTIONS-002
- Replaces: RULE-OPTIONS-002
- Reason: The original opens its registry key read-only for loading and for writing, so no option
  is ever saved, and a missing value takes the previous value's data from a shared buffer. The
  rebuild writes a checked per-user file in one step. A file that is missing, unreadable or of an
  unknown version, or that lacks a field its version requires, is read as the defaults as a whole,
  so no option takes another's value. A field a later version added takes its own default when an
  older file is upgraded, and the lobby presentation, Intro only once and the preferred scenario,
  which a file of the current version may leave out, take their own defaults when it does.
- Setting: None
- Default: mandatory
- Justification: The Options menu was written to keep the player's choices, and the original loses
  them only because of the two bugs. Nobody gains from choosing the options again at every launch.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.Persistence.cs,
  tests/Rechaos.Tests/GamePreferencesStoreTests.cs
- Dropped: no

## DEV-OPTIONS-002

- Departs from: RULE-OPTIONS-001
- Reason: Slide Panels starts off. The original initializes it to on, and panel motion holds
  input for about a quarter of a second on every panel change.
- Setting: Slide Panels, inverted (the deviation on starts Slide Panels off; Slide Panels on is the
  original's motion)
- Default: on
- Justification: Panel motion holds input for about a quarter of a second on every panel change and
  changes nothing in the match. A player who wants the original's motion switches Slide Panels on.
- Dropped: no

## DEV-OPTIONS-003

- Departs from: RULE-OPTIONS-001, SCR-UI-009
- Reason: The rebuild starts in a window, and F11 or Alt+Enter switches to borderless full screen
  from any screen; the choice is kept for every match. The original initializes full screen to on.
- Setting: Full screen (F11), inverted (the deviation on starts the rebuild in a window; full
  screen is the original's start)
- Default: on
- Justification: A window leaves the player's other programs reachable, full screen is one key away
  from any screen, and nothing in the match depends on it. A player who wants the original's start
  switches to full screen, and the choice is kept.
- Dropped: no

## DEV-NET-001

- Departs from: SCR-NET-001, SCR-NET-002, SCR-NET-003, SCR-NET-004, SCR-NET-005, RULE-COMLINK-001, FMT-SAVE-001, SCR-UI-003
- Replaces: SCR-NET-001, SCR-NET-002, SCR-NET-003, SCR-NET-004, SCR-NET-005
- Reason: The original's network play, its lobbies, its protocols and its WinSock, TAPI and serial
  paths are not reproduced. Online play uses a new coordination server, so no Comlink message is
  sent to or received from another computer in the original's form, and the network form of the
  save file has no counterpart. An online match that ends opens the awards without the final view
  of the city the original gives each player at its own computer.
- Setting: None
- Default: mandatory
- Justification: The original's WinSock, TAPI and serial paths cannot reach anything a current
  player can connect to, so there is nothing to keep, and the coordination server is what makes
  online play possible at all.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.Persistence.cs
- Dropped: no

Decided 2026-09-10 ("Networking scope").
