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

## Step 9: Remaining rule details

- Fix the rule differences docs/STATE-MAPPING.md found, verifying each against the spec first:
  - RULE-TURN-004, RULE-HEAL-001: a recurring Heal stays until the next turn start's Force test.
  - RULE-UPKEEP-001: after a Control takeover the new owner is paid the old yield once.
  - RULE-SITE-001, RULE-CHAOS-002: the headquarters' +2 Tolerance applies in a neutral sector.
  - RULE-AI-005, RULE-AI-026: the computer's local tech cap ignores who owns the sector.
  - RULE-POLICE-003, RULE-SETUP-005: the permanent crackdown value 100 never counts down.
  - RULE-AI-005, RULE-AI-013: record 0's `definition` as the owner the computer reads at index 64.
  - RULE-COMBAT-004: `force_start` of an attacker killed by a retaliation larger than its Force.
  - RULE-COMLINK-004: every inbox is emptied when a match is entered.
  - RULE-EVENT-005, RULE-EVENT-006: one Influence report per completed site.
  - RULE-AI-010: reverting a surplus hunter keeps a `needs_family` flag set earlier in the pass.
  - FMT-STATE-008: compare the gang entries with RULE-COMBAT-004.

## Step 10: Screens

The screens and interface rules were compared with the findings #181 recorded. What is left
needs static reads or captures of the original:

- RULE-TIMER-004: how much lighter a flash copy is and the order of the city-cell copies
  (FND-UI-017); the pressed faces of Influence's Escape, Move and Research, which no finding
  records.
- SCR-GANG-001's half-tone pattern (its PLACEHOLDER), SCR-GIVE-001's list background and dimming
  pattern, SCR-MOVE-001's table of disabled cells, and SCR-COMBAT-002's police portrait and
  header strips.

## Step 11: Deviations for what the rebuild does not reproduce

FMT-DATA-005 (DATA.Z) stays `unknown` while bytes of its header and file entries are not
interpreted (FND-DATA-009 lists them); an InstallShield 3 extraction of the blocks would settle
them and show whether they expand to the installed files.

## Step 12: Close the research plans

- `static_validation_plan.md`: the remaining items, most of which need a run of the original
  (the Force track y of SCR-COMBAT-002, the BUG-AI-006 threshold, CreatePalette in 8-bit, the save
  dialog's extension, the option value longer than four bytes).
- `manual_validation_plan.md`: its items become `EXP-` entries with experiment fixtures. A result
  that contradicts a rule reopens the matching step above.

## Step 13: From complete to validated

A row is `validated` only when its Tests column lists a test that compares the rebuild with
evidence from the original (see [docs/VALIDATION.md](docs/VALIDATION.md#tests-against-the-original)).

- Formats: FMT-DATA-001 to 003, FMT-GFX-001 to 003, FMT-VIDEO-001 and FMT-HELP-001 to 002 are
  validated against every shipped file. FMT-AUDIO-001 and 002 have file tests too, but the
  rebuild hands those files to the framework whole, so no decoder of its own is compared.
- Rules next, as each `EXP-` fixture from step 12 lands: a test replays the fixture and compares
  events and end state. A test that reaches a `mandatory` deviation cites its ID and allows for it.
- Screens last, against captures of the original, with masks for the areas a deviation draws.
