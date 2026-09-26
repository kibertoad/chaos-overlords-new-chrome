# Parity achievement plan

The steps that take every row of [PARITY.md](PARITY.md) to `complete`, and then to `validated`,
in the order they should be done. Written on 2026-09-25, after the static validation work of #181.
At that point 117 rows were `complete`, 85 `partial` and 20 `missing`, no row listed a test that
compares the rebuild with the original, and `spec/experiments/` held no `EXP-` entry.

Each step names the spec entries it closes. A step is done when the code cites those IDs, the
`PARITY.md` rows are updated, `node tools/check-spec.mjs --check` passes and the fast gate
(`tools/Invoke-Validation.ps1`) is green. Delete a step from this file when it is done, as the
validation plans do with their items.

## Tooltips follow the mechanics

Every mechanic nuance a step brings in line with the original is written into the hover text of
the commands and statistics it affects, in the same change. A player reads the tooltip to learn
how a rule works, so a tooltip that still describes the rebuild's old behaviour after a step is a
defect of that step. The texts live in `CommandActionTooltips` (the command overlay),
`InformationEffectTooltips` (statistics and sites), `GangStatisticModifierTooltip` (the
statistic breakdown), `StatusConsoleUi` (Tolerance and the sector console) and
`PlayerRankingTooltip`. Each step below lists the tooltips it must revisit. A tooltip states the
original's rule in concrete terms (the numbers, the order, when it applies) and cites the spec ID
in a code comment next to the text.

## Version rules for this plan

Most gameplay steps change how a turn resolves, so they bump `MULTIPLAYER_SESSION_VERSION` and
`MultiplayerSessionVersion.Current` together. A step that changes the state encoding also moves
`MatchStateHasher.FormatVersion`, `NativeSaveSerializer.CurrentFormatVersion` and
`MatchReplaySerializer.CurrentFormatVersion`, as `StateFingerprintVersionCouplingTests` requires.
To retire live matches once instead of once per step, land steps 2 to 9 on one branch and release
them together with a single session bump, or accept one bump per release.

## Step 1: Settle the open decisions

These decide the shape of later steps, so they come first.

- Four `mandatory` deviations change rule or AI results, where the Fidelity rules ask for a
  setting that starts `off`: DEV-EQUIP-001 (cash in submission order), DEV-CONTROL-001 (only
  players with a Control order compete), DEV-AI-001 (corrected hunter guard) and DEV-AI-002
  (unplayable AI actions dropped). Decide for each whether it becomes a setting with the
  original's behaviour as the default. Turning one into a setting adds the original's path to
  the code and lets the validation suite reach it. Record the outcome in `docs/DECISIONS.md`.
- Decide how the in-memory layouts FMT-STATE-001 to FMT-STATE-009 count as `complete`: either a
  documented field-by-field equivalence in the row's notes, or a deviation for each place the
  rebuild stores something different (the Force-0 marker for an empty roster slot where the
  original writes sector 100 is one, RULE-GANG-002).

Tooltips: the Equip tooltip already says purchases follow the player's click order. If
DEV-EQUIP-001 becomes a setting, the text follows the setting, and with the setting off it
describes the scan by player slot and roster slot.

## Step 2: Split Tolerance into base and site parts

Closes RULE-TOLERANCE-001, RULE-TOLERANCE-002, RULE-TURN-003, RULE-BRIBE-001 and
RULE-SNITCH-001, and settles the base Tolerance question of FMT-STATE-002.

- Keep a base Tolerance per sector apart from the sites' Tolerance.
- At the start of each resolution, move the base one point toward 17 minus the sector's base
  Income. Remove the Upkeep drift toward a normal value that includes the sites.
- After the instant phase, clamp every base Tolerance to 1..40.
- Bribe adds 3 and Snitch subtracts 3 from the base, wrapping as the original's byte does.
  Today the rebuild's single Tolerance lets a Bribe or Snitch reach the same turn's Chaos test;
  after the split, the Chaos test sees the change only where the entries say it does.

This changes the state encoding: bump all four versions listed above.

Tooltips: Bribe and Snitch (`CommandActionTooltips`) currently say Tolerance drifts during
Upkeep toward a normal value, and show the shift against that normal value. Rewrite them to state
the drift at the start of resolution toward 17 minus base Income, the 1..40 clamp after the
instant phase, and when the change first reaches a Chaos test. The Tolerance tooltip of the
sector console (`StatusConsoleUi.Tolerance`) shows the base and the sites' part separately.

## Step 3: Police, Chaos and Control

Closes RULE-POLICE-002, RULE-CHAOS-002 and RULE-CONTROL-001. Depends on step 2, because the
Crackdown test reads the new Tolerance.

- Police presence and its draw are added only on the third Crackdown within five turns, the one
  that neutralizes the sector (FND-POLICE-004). Neutralization clears only the three site
  progress bytes, not Support, Tolerance modifiers or resistance (FND-CHAOS-002).
- Chaos pays in a sector under police presence unless that sector cracked down this turn
  (FND-CHAOS-002). Check whether gangs killed in this turn's combat are skipped, and match it.
- Control settles only contested sectors without police, and adds the owner's defense to the
  owner's own pool (FND-CONTROL-003). DEV-CONTROL-001 and DEV-CONTROL-002 stay as step 1 decides.

Tooltips: Chaos (when it pays and when a Crackdown follows), Control (which sectors are settled,
how the owner's defense is pooled, the tie rule), and the Control statistic in
`InformationEffectTooltips`, which says it adds to sector defense.

## Step 4: Attack and the Combat statistic

Closes RULE-ATTACK-001, RULE-AI-016, RULE-COMBAT-001 and RULE-GANG-001.

- An evaded attack still lowers the target player's attitude toward the attacker by the reaction
  (FND-AI-047).
- The attacker's Martial Arts test is `== 0`, not `> 0` (FND-COMBAT-008).
- Store the weapon skills in Combat when a gang's statistics are rebuilt, not when an attack is
  computed (FND-GANG-007), so every reader of the stored Combat sees the same value.

Stored Combat changes the state encoding: bump all four versions.

Tooltips: Attack (retaliation and the Martial Arts case), the Combat, Strength, Blade, Range,
Fighting and Martial Arts statistics, and the Combat line of the statistic breakdown, which must
show the weapon skills as part of the stored value.

## Step 5: Comlink and events

Closes RULE-COMLINK-007 and the unchecked parts of RULE-EVENT-003, RULE-EVENT-004,
RULE-EVENT-012, RULE-EVENT-013, RULE-EVENT-014 and FMT-STATE-006.

- When a player finishes planning, drop the read messages at the front of the inbox.
- Compare each report's recipients, type and arguments with the entries: the elimination report
  to all six slots, Crackdown recipients by gang presence at the start of resolution, and the
  control and failed-Equip reports. Fix what differs.

Tooltips: the Comlink inbox's hover text says when read messages are dropped.

## Step 6: Objectives, endgame and awards

Closes RULE-OBJECTIVE-004, RULE-OBJECTIVE-005, RULE-AWARDS-002, SCR-AWARDS-002 and
SCR-OBJECTIVE-001.

- Big 40, Siege, Big Man and Armageddon count every slot, not only living players. Remove the
  rebuild's own Kill 'Em All and Eliminate end tests (FND-OBJECTIVE-003). Compare the timed test.
- Elimination: start the endgame music over the card; when every local human is eliminated,
  return to the title as the original does; show the Ready card in the original's order; show the
  awards when a lone human's elimination comes on the turn the match would have ended.
- The victory splash goes to the lone active player, computer included, on the Awards tab
  (FND-AWARDS-004).
- Player Rankings places each portrait in proportion to its score's distance from the leader,
  over 140 pixels (FND-OBJECTIVE-005).

Tooltips: the scenario descriptions in `ScenarioSetupTooltip` (each scenario's end condition as
the original tests it) and `PlayerRankingTooltip` (what the rail position means).

## Step 7: Setup

Closes RULE-SETUP-002, RULE-SETUP-009, RULE-SETUP-010 and the unchecked order of
RULE-SETUP-008.

- Keep a stored scenario preference, Greed when nothing is stored.
- Portrait arrows skip the portraits other slots hold; Add gives the new human the lowest free
  portrait; a later setup reopens with the roster of the last Begin (FND-SETUP-013).
- Check the order of the Ready card, Game Information, combat results and Last Turn Events at the
  start of planning.

Tooltips: the setup tooltips for the portrait arrows, Add and the scenario selector.

## Step 8: Computer players

Closes RULE-AI-001, RULE-AI-002, RULE-AI-005, RULE-AI-006, RULE-AI-010, RULE-AI-013 and
RULE-AI-019 to RULE-AI-031. Comes after steps 2 to 7 because the handlers read the rules those
steps change.

- Dispatcher: the `needs_family` gate, the record reset, family 99 for a blank table cell and the
  Big Man first-turn hire role (FND-AI-041, FND-AI-042).
- Hire placement keeps anchor 63 for player 0 while sector 0, 6, 7 or 8 is free land
  (FND-AI-051); check the per-scenario slot adjustments and the hunter reversion (FND-AI-050).
- Families 0 and 4 count previous Chaos where the rebuild counts Hide, and group the previous
  actions as the jump table does (FND-AI-046, FND-AI-048, FND-AI-049).
- Upgrades follow FND-AI-055: the weapon choice starts from the equipped weapon, family 10's
  armor is chosen by Stealth, families 11 and 12 compare Detect and families 13 and 14 compare
  Control.
- Families 1, 2, 3, 6, 7, 11, 12, 13 and 14: the details each row lists (the `needs_family`
  write with the Greed Terminate, the owner queries, the end marker 100 of the guard list, the
  five contested draws and the unset Support threshold of FND-AI-062 and BUG-AI-006).
- Sector selector mode 4 (FND-AI-056) against the rebuild's version.

Both AI policies are affected; DEV-AI-003's Advanced AI keeps the original planner's commands.

Tooltips: the Game Information panel's AI policy label and any hover text that describes how
computer players choose orders.

## Step 9: Remaining rule details

- RULE-TURN-005: record whether the sector-wide order leaves Research out of the recurring
  choices, and match it.
- RULE-AUDIO-006: the turn-start sound plays only where the original plays it; check it with
  effects off (BUG-AUDIO-001).
- RULE-RNG-001, RULE-TERMINATE-001 and RULE-GANG-002: their differences are covered by
  deviations or by step 1's representation decision; confirm nothing else differs and mark them
  `complete`.

## Step 10: Screens

Closes the partial and missing screen rows. The comparisons use the findings #181 recorded.

- SCR-RESEARCH-001: the list's press region starts at panel y 26.
- SCR-HIRE-002: portraits one pixel right and two pixels down; the hire and snub marks cropped
  from the recorded rectangles.
- SCR-ATTACK-001 (FND-ATTACK-003, FND-ATTACK-004), SCR-COMBAT-001 (FND-COMBAT-012) and
  SCR-COMBAT-002 (FND-COMBAT-010): panel origin, control faces, keys, portraits and tracks.
- SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001, SCR-MOVE-001, SCR-FINANCE-001, SCR-GANG-001,
  SCR-GANG-002, SCR-UI-002 and SCR-UI-005: pin the controls, fonts, keys and rows each row lists.
- RULE-UI-003, RULE-UI-005, RULE-UI-006, RULE-UI-007, RULE-UI-008 and RULE-UI-010: the slide-out,
  meter arithmetic, marker precedence, wait cursor, the 166 ms tick and the gang lists.
- RULE-GFX-002, RULE-TIMER-004, RULE-UI-013 and RULE-UI-014: compare the display, presentation
  waits, program shell and input loop with the rebuild, then mark or fix.

Where a screen already carries a deviation for added information (tooltips, highlights,
breakdowns), the row becomes `complete` once everything outside that deviation matches.

## Step 11: Deviations for what the rebuild does not reproduce

These rows stay `missing` until a deviation covers them:

- SCR-NET-001 to SCR-NET-005: the legacy network screens.
- SCR-UI-009: the Windows menu bar, whose commands live in the Escape menu, Options and
  shortcuts.
- FMT-SAVE-002: the short save file. FMT-SAVE-001 already cites DEV-SAVE-001 and DEV-NET-001.
- FMT-DATA-004: either read CLT00002 or record why the rebuild does without it.
- RULE-AUDIO-010 and RULE-HELP-001 already cite DEV-AUDIO-001 and DEV-HELP-001; mark them
  `complete` once the rest of each rule matches.

FMT-DATA-005 (DATA.Z) is `unknown` in the spec and cannot be `complete`; it needs static work
first.

## Step 12: Close the research plans

- `static_validation_plan.md`: the remaining items, most of which need a run of the original
  (the Force track y of SCR-COMBAT-002, the BUG-AI-006 threshold, CreatePalette in 8-bit, the save
  dialog's extension, the option value longer than four bytes).
- `manual_validation_plan.md`: its items become `EXP-` entries with experiment fixtures. A result
  that contradicts a rule reopens the matching step above.

## Step 13: From complete to validated

A row is `validated` only when its Tests column lists a test that compares the rebuild with
evidence from the original (see [docs/VALIDATION.md](docs/VALIDATION.md#tests-against-the-original)).

- Formats first, since they need no new experiments: tests that decode every shipped file for
  FMT-DATA-001 to FMT-DATA-003, FMT-GFX-001 to FMT-GFX-003, the audio, video and help formats.
- Rules next, as each `EXP-` fixture from step 12 lands: a test replays the fixture and compares
  events and end state. A test that reaches a `mandatory` deviation cites its ID and allows for it.
- Screens last, against captures of the original, with masks for the areas a deviation draws.
