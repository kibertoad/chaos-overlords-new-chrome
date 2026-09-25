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
To retire live matches once instead of once per step, land the gameplay steps on one branch and release
them together with a single session bump, or accept one bump per release.

DEV-EQUIP-001, DEV-CONTROL-001, DEV-AI-001 and DEV-AI-002 are mandatory and stay so (the
2026-09-26 decision in `docs/DECISIONS.md`); no step adds the original's path for them.

## Step 8: Computer players

Closes RULE-AI-001, RULE-AI-002, RULE-AI-005, RULE-AI-006, RULE-AI-010, RULE-AI-013 and
RULE-AI-019 to RULE-AI-031. Comes after steps 2 to 7 because the handlers read the rules those
steps change.

- Hire placement keeps anchor 63 for player 0 while sector 0, 6, 7 or 8 is free land
  (FND-AI-051); check the per-scenario slot adjustments and the hunter reversion (FND-AI-050).
- Families 0 and 4 count previous Chaos where the rebuild counts Hide, and group the previous
  actions as the jump table does (FND-AI-046, FND-AI-048, FND-AI-049).
- Upgrades follow FND-AI-055: the weapon choice starts from the equipped weapon, family 10's
  armor is chosen by Stealth, families 11 and 12 compare Detect and families 13 and 14 compare
  Control.
- Families 1, 2, 6, 11, 13 and 14: the details each row lists (family 1's `needs_family`
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
  deviations or by the representation decision of 2026-09-26; confirm nothing else differs and
  mark them `complete`.
- FMT-STATE-001 to FMT-STATE-009: map every field a rule reads or writes to the rebuild state
  that holds it, as the 2026-09-26 decision asks, and mark each row by what the mapping shows.

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
