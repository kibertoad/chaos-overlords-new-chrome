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
test that reaches a mandatory deviation cites its ID and allows for it.

Dated product decisions behind many of these entries, with their full reasoning, are in
[docs/DECISIONS.md](docs/DECISIONS.md).

## DEV-HELP-001

- Departs from: FMT-HELP-001, SCR-UI-009, RULE-HELP-001
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

- Departs from: FMT-VIDEO-001
- Reason: The rebuild decodes the two shipped movies with its own decoder for the subset of the
  format they use. A file that is malformed or uses anything outside that subset is skipped and
  play goes on to the title screen.
- Setting: None
- Default: mandatory
- Justification: It changes only what happens where the original would fail to play the file, so
  there is nothing after that point to keep or compare.
- Dropped: no

Decided 2026-09-13.

## DEV-SAVE-001

- Departs from: FMT-SAVE-001, FMT-SAVE-002
- Reason: The rebuild neither reads nor writes the original's save files. It keeps its own save
  format, with a version number and bounded readers.
- Setting: None
- Default: mandatory
- Justification: What a player can do in a match is the same whichever format holds it, and the
  rebuild's format adds a version number and bounded readers. A setting would need a reader and
  writer for the original's format, which is a separate scope decision (2026-09-10).
- Dropped: no

Decided 2026-09-10 ("Save compatibility scope").

## DEV-RNG-001

- Departs from: RULE-RNG-001, RULE-OPTIONS-001
- Reason: A local game is seeded from the low 16 bits of the rebuild's own uptime clock when the
  game object is created, in place of `timeGetTime` at process start. Replays, tests and online
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
  The original fails a hire when its cost is greater than cash, which refuses that hire.
- Setting: None
- Default: mandatory
- Justification: A hire that costs nothing takes nothing from cash, so refusing it because cash is
  already negative protects nothing and only keeps a player in debt from rebuilding. It adds an
  option and removes none.
- Dropped: 2026-09-25, the original also lets a zero-cost hire through while cash is negative: it
  skips the cash test when the cost is 0 (FND-HIRE-006, RULE-HIRE-001)

The Reason above rested on reading the cash test without the zero-cost branch in front of it.

## DEV-RESEARCH-001

- Departs from: SCR-RESEARCH-001
- Reason: The Research panel shows the research accumulated so far beside each item's total.
- Setting: None
- Default: mandatory
- Justification: It adds information to the panel and changes nothing else.
- Dropped: no

## DEV-MOVE-001

- Departs from: RULE-MOVE-001, SCR-MOVE-001
- Reason: The rebuild refuses a Move, when it is ordered, into a sector that already holds six
  of the player's gangs. Whether the original's panel refuses it too is not recorded; if it does
  not, the original accepts the order and RULE-MOVE-002 sends the gang back at resolution.
- Setting: None
- Default: mandatory
- Justification: The player learns at once that the gang cannot enter, where the original accepts
  the order and then sends the gang back at resolution with its turn spent. A gang the player could
  have given a useful order instead is no longer wasted on a Move that cannot happen.
- Dropped: no

Whether the original's panel refuses the order too is in `manual_validation_plan.md`. The rebuild
counts only the gangs already in the destination, so moving one gang out and another in to a full
sector takes two turns where the original allows one; counting gangs ordered out of the
destination, as DEV-HIRE-001 does, would remove that difference.

## DEV-CONTROL-001

- Departs from: RULE-CONTROL-001, BUG-CONTROL-001
- Reason: Only players who ordered Control in the sector compete for it, with the original's
  arithmetic and tie draw among them. In the original a player with no Control order can take a
  sector when its defence sum is negative.
- Setting: None
- Default: mandatory
- Justification: A sector goes only to a player who ordered Control of it, as the manual describes
  the rule. In the original, a player with no order there is handed the sector when negative Support
  drags its sum below zero, and ties with a real challenger who would otherwise win. No player
  source relies on it, and a player who wants the sector can still order Control.
- Dropped: no

The fix changes which player owns the sector when the case arises.

## DEV-CONTROL-002

- Departs from: RULE-CONTROL-001
- Reason: When a Crackdown made by this turn's Chaos is in force as the Control pass runs, the
  original leaves the sector out of Control without a word. The rebuild records a failed Control
  result for it. The owner does not change in either case.
- Setting: None
- Default: mandatory
- Justification: It adds a report of what happened. The owner of the sector is the same in both.
- Dropped: no

## DEV-GANG-001

- Departs from: SCR-GANG-002
- Reason: Hovering one of a live gang's fourteen statistics shows its base value and one signed
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
- Justification: The original's result depends on roster slots the player cannot see while giving
  orders; the rebuild's follows the order the player gave. Every outcome of the original stays
  reachable, since giving Sell and Equip in slot order reproduces the original's scan.
- Dropped: no

## DEV-EQUIP-002

- Departs from: SCR-EQUIP-001
- Reason: The purchase panel draws the gang's held items in three 20-by-20 boxes under the
  portrait, and a double-click on the portrait opens the gang information panel and returns to
  the same selection.
- Setting: None
- Default: mandatory
- Justification: It adds a display of the items the gang holds and a shortcut, and takes nothing
  away.
- Dropped: no

## DEV-GIVE-001

- Departs from: SCR-GIVE-001
- Reason: Up and Down cycle the Give recipient.
- Setting: None
- Default: mandatory
- Justification: It adds keys, and the mouse works as before.
- Dropped: no

## DEV-FINANCE-001

- Departs from: SCR-FINANCE-001, RULE-FINANCE-001
- Reason: The Finance projection lists sector tax, site Cash and gang Upkeep as separate
  components where the original draws its eight rows.
- Setting: None
- Default: mandatory
- Justification: The amounts are the ones the rules compute, and no order depends on how the
  projection is broken down. Listing each component shows the player where the money comes from.
- Dropped: no

Which of the original's rows holds which amount is not recorded, so how far the two differ is not
known yet; the static check is in `static_validation_plan.md`.

## DEV-ATTACK-001

- Departs from: SCR-ATTACK-001
- Reason: A double-click on a target cell, without moving, opens the enemy gang's information
  panel and returns to the picker.
- Setting: None
- Default: mandatory
- Justification: It adds a shortcut to a panel the player can already open.
- Dropped: no

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
- Dropped: no

The correction applies under the Original AI policy as well. Decided 2026-09-17.

## DEV-AI-002

- Departs from: RULE-AI-002, RULE-AI-019, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-023, RULE-AI-024, RULE-AI-025, RULE-AI-026, RULE-AI-027, RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031, RULE-MOVE-002
- Reason: When a family handler plans an action that has no legal equivalent in the rebuild's
  command queue, the rebuild gives the gang no command and keeps the planned action in its
  planning state. The original stores the action in the gang record and resolves it. The usual
  case is a Move to the gang's own sector, which the sector selector returns when capacity blocks
  every step. The gang's planning history is the same; its resolved action can differ.
- Setting: None
- Default: mandatory
- Justification: A computer player's gang is held to the same legal orders as a human's, so it
  cannot carry out an action no player could order. In the usual case, a Move to the gang's own
  sector, the gang stays where it is either way, and its planning history, which later turns read,
  is kept.
- Dropped: no

The resolved action can differ from the original's, which changes the match when it does. Decided
2026-09-17.

## DEV-AI-003

- Departs from: RULE-AI-001, RULE-AI-002, RULE-AI-019, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-023, RULE-AI-024, RULE-AI-025, RULE-AI-026, RULE-AI-027, RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031, RULE-UI-009
- Reason: A second computer-player policy keeps every command the original planner chooses and
  gives an idle gang at most one legal, affordable fallback of its own scoring, examining gangs in
  ascending order. Resolution odds are unchanged. Game Information names the policy after the
  Mentality text.
- Setting: Advanced AI (Original is the original planner)
- Default: off
- Dropped: no

The policy is chosen for a new match and kept by it; loaded saves keep the policy they were
started with.

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
  only to Enter and Execute.
- Setting: None
- Default: mandatory
- Justification: It adds keys, and Enter and Execute work as before.
- Dropped: no

## DEV-UI-001

- Departs from: RULE-UI-003, SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008, SCR-OPTIONS-001
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
- Justification: It adds information to the minimap and changes nothing else.
- Dropped: no

## DEV-UI-003

- Departs from: SCR-UI-004, SCR-MOVE-001
- Reason: A ctrl-click picks several gang cards, and Attack, Control, Heal, Hide, Influence or
  Move is then given to all of them at once. Each gang is validated on its own, and a bulk Move
  counts the whole selection against the destination's room before queueing.
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
- Dropped: no

When the original plays the movies is not yet recorded (`manual_validation_plan.md`).

## DEV-UI-005

- Departs from: SCR-UI-003, SCR-UI-004, SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008
- Reason: Hover tooltips explain statistics, attributes, modifiers, modes, options and ranking
  scores, and a two-second rest on a command explains the order.
- Setting: None
- Default: mandatory
- Justification: It adds information on hover and changes nothing else.
- Dropped: no

## DEV-UI-006

- Departs from: SCR-UI-003, SCR-FINANCE-001
- Reason: The city console shows next turn's projected cash beside the current Cash, as
  `CASH 20 [18] (+1)`: cash, the cash left after queued Bribe and Equip prices, and the change
  over the whole cycle, with a breakdown on hover.
- Setting: None
- Default: mandatory
- Justification: It adds a projection the player could work out from the Finance panel, and changes
  nothing else.
- Dropped: no

## DEV-UI-007

- Departs from: SCR-UI-004, RULE-UI-011
- Reason: Hovering the Tolerance value shows the range the player's queued Chaos can reach, and
  the value turns orange when that range can set off a Crackdown.
- Setting: None
- Default: mandatory
- Justification: It adds information the player could work out from the queued orders, and changes
  nothing else.
- Dropped: no

## DEV-UI-008

- Departs from: SCR-UI-004, SCR-ATTACK-001, SCR-MOVE-001
- Reason: The command pickers name valid targets, and hovering a gang with a queued Move,
  Influence or Attack highlights its target on the board.
- Setting: None
- Default: mandatory
- Justification: It adds information about orders the player has already given, and changes nothing
  else.
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

- Departs from: SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008, SCR-OPTIONS-001
- Reason: Panels accept keyboard navigation, and Escape and the right mouse button cancel them.
  The original's panels take Enter and Execute and, on the idle-gang warning, Escape.
- Setting: None
- Default: mandatory
- Justification: It adds keys and ways to cancel; the original's Enter and Execute work as before.
- Dropped: no

## DEV-UI-011

- Departs from: SCR-UI-009
- Reason: Saving and loading use nine named slots, and Escape opens a pause menu. The original
  saves and loads from its menu bar.
- Setting: None
- Default: mandatory
- Justification: Saving and loading stay available wherever the original allows them, and slots with
  names replace a file dialog that the original's menu bar opens.
- Dropped: no

## DEV-UI-012

- Departs from: SCR-UI-001
- Reason: The title screen shows the build version and a Report Bug control.
- Setting: None
- Default: mandatory
- Justification: It adds a version label and a way to report a bug, and changes nothing else.
- Dropped: no

## DEV-UI-013

- Departs from: SCR-UI-004
- Reason: The detailed-sector screen's portrait strip marks each opponent with detected gangs in
  the sector, and the player can page that opponent's detected gangs on the cards. The original
  lists only the viewer's own gangs.
- Setting: None
- Default: mandatory
- Justification: Only gangs the player has detected are shown, so what the player can know is
  unchanged; it saves looking them up elsewhere.
- Dropped: no

Decided 2026-09-18.

## DEV-UI-014

- Departs from: SCR-UI-004
- Reason: The detailed-sector screen shows how many police turns remain in the sector.
- Setting: None
- Default: mandatory
- Justification: It adds information and changes nothing else.
- Dropped: no

Whether the original shows the count is not recorded.

## DEV-OPTIONS-001

- Departs from: RULE-OPTIONS-001, RULE-OPTIONS-002, BUG-OPTIONS-001, BUG-OPTIONS-002
- Reason: The original opens its registry key read-only for loading and for writing, so no option
  is ever saved, and a missing value takes the previous value's data from a shared buffer. The
  rebuild writes a checked per-user file in one step and falls back to the default for each
  missing field.
- Setting: None
- Default: mandatory
- Justification: The Options menu was written to keep the player's choices, and the original loses
  them only because of the two bugs. Nobody gains from choosing the options again at every launch.
- Dropped: no

## DEV-OPTIONS-002

- Departs from: RULE-OPTIONS-001
- Reason: Slide Panels starts off. The original initializes it to on, and panel motion holds
  input for about a quarter of a second on every panel change.
- Setting: Slide Panels
- Default: on
- Justification: Panel motion holds input for about a quarter of a second on every panel change and
  changes nothing in the match. A player who wants the original's motion switches Slide Panels on.
- Dropped: no

## DEV-OPTIONS-003

- Departs from: RULE-OPTIONS-001, SCR-UI-009
- Reason: The rebuild starts in a window, and F11 or Alt+Enter switches to borderless full screen
  from any screen; the choice is kept for every match. The original initializes full screen to on.
- Setting: Full screen (F11)
- Default: on
- Justification: A window leaves the player's other programs reachable, full screen is one key away
  from any screen, and nothing in the match depends on it. A player who wants the original's start
  switches to full screen, and the choice is kept.
- Dropped: no

## DEV-NET-001

- Departs from: SCR-NET-001, SCR-NET-002, SCR-NET-003, SCR-NET-004, SCR-NET-005, RULE-COMLINK-001, FMT-SAVE-001
- Reason: The original's network play, its lobbies, its protocols and its WinSock, TAPI and serial
  paths are not reproduced. Online play uses a new coordination server, so no Comlink message is
  sent to or received from another computer in the original's form, and the network form of the
  save file has no counterpart.
- Setting: None
- Default: mandatory
- Justification: The original's WinSock, TAPI and serial paths cannot reach anything a current
  player can connect to, so there is nothing to keep, and the coordination server is what makes
  online play possible at all.
- Dropped: no

Decided 2026-09-10 ("Networking scope").
