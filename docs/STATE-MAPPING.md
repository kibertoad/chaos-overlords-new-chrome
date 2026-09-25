# In-memory state mapping

Status: first pass, 2026-09-25

The 2026-09-26 decision on in-memory layouts in [DECISIONS.md](DECISIONS.md) says a
`FMT-STATE` entry counts as `complete` in [PARITY.md](../PARITY.md) when every field a rule
reads or writes is mapped to the rebuild state that holds the same value at the same point. This
document is that mapping for FMT-STATE-001 to FMT-STATE-009. Each table lists the fields of one
layout, the rules that read or write them, the rebuild state that holds the value, and one of these
verdicts:

- `same`: the rebuild holds the same value at the same point.
- `representation`: the rebuild encodes the value differently, and no rule result can tell.
- `deviation DEV-...`: a deviation in [DEVIATIONS.md](../DEVIATIONS.md) covers the difference.
- `differs`: the rebuild holds a different value, or does not hold it where a rule reads it.
- `not checked`: the mapping was not verified against the code.

Rebuild state is named `Type.Member`; unless a path says otherwise the types are in
`src/Rechaos.Core/GameModel`. A roster slot is the gang's index in `MatchPlayerState.Gangs`.

## FMT-STATE-001: Gang record

| Field | Offset | Rules | Rebuild state | Match | Notes |
|---|---|---|---|---|---|
| roster slot (record index) | | RULE-HIRE-001, RULE-TURN-003, RULE-TURN-004, RULE-DETECT-001, RULE-AI-001 | Index in `MatchPlayerState.Gangs` | same | Right Hands at index 0. A hire reuses the first inactive index, otherwise appends. At most 80 active gangs, so index 80 is never filled, and slot 0 is reused once the Right Hands die. |
| `player` | `0x00` | RULE-ATTACK-001, RULE-ATTACK-002, RULE-CONTROL-001, RULE-HIRE-001, RULE-CITY-004, RULE-SETUP-006 | `MatchGangState.Owner` | same | |
| `definition` | `0x01` | RULE-GANG-001, RULE-HIRE-001, RULE-AI-029, RULE-COMBAT-002, RULE-UPKEEP-001, RULE-FINANCE-001 and others | `MatchGangState.DefinitionId` | same | |
| `sector` | `0x02` | RULE-GANG-002, RULE-HIRE-001, RULE-DETECT-001, RULE-TURN-003, RULE-TURN-004, RULE-MOVE-001, most AI rules | `MatchGangState.SectorId` with `MatchGangState.IsActive` (Force > 0) | representation | The original writes 100 on death; the rebuild keeps the last sector and marks the slot inactive with Force 0 (RULE-GANG-002). Every reader skips inactive records first. RULE-TURN-004 reads `sectors[100]` for a dead gang and discards the result. |
| `force` | `0x03` | RULE-GANG-002, RULE-HEAL-001, RULE-COMBAT-002, RULE-HIRE-001, RULE-TURN-004, RULE-TURN-005, RULE-AI-004 | `MatchGangState.Force` | representation | Same for live gangs. A dead gang holds 0 where the original keeps 0 or less, and a terminated gang holds 0 where the original keeps its Force. No rule reads the Force of an inactive record for a result. |
| `weapon` | `0x04` | RULE-EQUIP-001, RULE-EQUIP-002, RULE-GIVE-001, RULE-SELL-001, RULE-GANG-001, RULE-COMBAT-001, RULE-COMBAT-002, RULE-AI-005, RULE-AI-019 to RULE-AI-031 | `MatchGangState.WeaponItemId` | representation | null for -1. Kept on death and Terminate. |
| `armor` | `0x05` | as `weapon` | `MatchGangState.ArmorItemId` | representation | null for -1. Kept on death. |
| `misc` | `0x06` | RULE-EQUIP-001, RULE-EQUIP-002, RULE-GIVE-001, RULE-SELL-001, RULE-GANG-001, RULE-COMBAT-002, RULE-AI-005, RULE-AI-028 | `MatchGangState.MiscellaneousItemId` | representation | null for -1. Kept on death. |
| `action` | `0x07` | RULE-TURN-002 to RULE-TURN-005, RULE-HIDE-001, RULE-ATTACK-001, RULE-COMBAT-002, RULE-AI-004 and the action rules | `MatchGangState.QueuedCommand` (its `GameCommand.Action`) | representation | No command is held for action 0. A one-off order is dropped at the end of resolution, where the original clears it at turn start; no rule reads `action` in between. |
| Hide in force (`action` 8) | `0x07` | RULE-HIDE-001, RULE-ATTACK-001, RULE-POLICE-001 | `MatchGangState.Hidden` | representation | Set from the command on submit and cancel, and after the turn-start step. It always equals "action is Hide". |
| `target` | `0x08` | RULE-TURN-005, RULE-ATTACK-001, RULE-MOVE-001, RULE-EQUIP-001, RULE-RESEARCH-001, RULE-INFLUENCE-001, RULE-GIVE-001, RULE-SELL-001, RULE-COMBAT-002, RULE-AI-004 | `GameCommand.Target`, plus `SecondaryTarget` to `QuaternaryTarget` for Give and Sell | representation | Attack names the target by `GangId`. Influence uses the global site number (sector * 3 + slot). Give and Sell name the equipped items where the original holds a slot mask; the order the mask bits are handled in was not checked. |
| `target_2` | `0x09` | RULE-ATTACK-001, RULE-ATTACK-002, RULE-GIVE-001, RULE-COMBAT-002, RULE-TURN-005, RULE-AI-001, RULE-AI-004 | Folded into the `GangId` of `GameCommand.Target` | representation | Attack target and Give recipient. |
| `repeat_action` | `0x0A` | RULE-TURN-004, RULE-TURN-005, RULE-HIDE-001, RULE-HIRE-001 | `GameCommand.Repeat` with `GameCommand.Action`; cleared by `MatchState.NormalizeRecurringCommands` and `MatchState.ShouldStopResolvedCommand` | differs | The turn-start tests of RULE-TURN-004 match. `ShouldStopResolvedCommand` also cancels a recurring Heal after the instant phase once Force is 10, where the original tests Force only at the next turn start. A gang healed to 10 and then hurt in combat keeps healing in the original and stops in the rebuild. DEV-TURN-001 covers only the refused recurring Bribe and Snitch. |
| `repeat_target` | `0x0B` | RULE-TURN-004, RULE-TURN-005, RULE-HIRE-001 | `GameCommand.Target` of a recurring command | representation | Only Influence and Research read it. |
| `visible_to` | `0x0C` | RULE-DETECT-001 and RULE-HIRE-001 (write), RULE-ATTACK-002, RULE-AI-003, RULE-AI-004, RULE-AI-029, RULE-UI-010 | `MatchState.CanPlayerDetectGang`, computed on each call | representation | Same inputs as RULE-DETECT-001. The original stores a snapshot at planning entry; positions and stored statistics do not change during planning, so every reader sees the same value. |
| `combat` | `0x12` | RULE-GANG-001, RULE-COMBAT-001, RULE-ATTACK-001, RULE-AI-004, RULE-AI-005, RULE-HIRE-001 | `MatchGangState.StoredStatistics` (`Combat`) | same | Rebuilt for active gangs by `EffectiveStatisticsCalculator.RebuildBeforePlanning` after the site rebuild; a recruit takes its definition's values. |
| `defense` to `martial_arts` | `0x13` to `0x1F` | RULE-GANG-001, RULE-DETECT-001, RULE-CHAOS-001, RULE-CONTROL-001, RULE-HEAL-001, RULE-INFLUENCE-001, RULE-RESEARCH-001, RULE-COMBAT-001, RULE-AI-004, RULE-AI-005, RULE-HIRE-001 | `MatchGangState.StoredStatistics` (the other thirteen members) | same | Written only by the rebuild before planning and at hire. The INT8 wrap of a total outside -128 to 127 is not reproduced (see the RULE-GANG-001 row). |

Verdict: stays `partial`. `repeat_action` differs for a recurring Heal that reaches Force 10 in
the instant phase (RULE-TURN-004, RULE-HEAL-001). The Give and Sell mask order is not checked.

## FMT-STATE-002: Sector record

| Field | Offset | Rules | Rebuild state | Match | Notes |
|---|---|---|---|---|---|
| `owner` | `0x00` | RULE-CONTROL-001, RULE-POLICE-002, RULE-SITE-001, RULE-UPKEEP-001, RULE-EQUIP-003, RULE-HIRE-003, RULE-CITY-003, RULE-TURN-006, most AI rules | `MatchSectorState.Owner` | representation | null for -1. |
| `base_income` | `0x01` | RULE-CITY-001, RULE-SITE-001, RULE-TOLERANCE-001 | `MatchSectorState.Income` | representation | Nothing writes `base_income` or `income` after generation, so one member holds both. |
| `base_tolerance` | `0x02` | RULE-CITY-001, RULE-BRIBE-001, RULE-SNITCH-001, RULE-TOLERANCE-001, RULE-TOLERANCE-002, RULE-SITE-001 | `MatchSectorState.BaseTolerance` | same | Bribe and Snitch store the low byte (`ToleranceResolver.ApplyBribe`, `ApplySnitch`). |
| `cash_yield` | `0x03` | RULE-SITE-001 (writes), RULE-UPKEEP-001, RULE-FINANCE-001, RULE-UI-011 | `SectorIncomeResolver.SectorCash`, computed at Upkeep from `MatchSiteState.InfluencedBy` | differs | The original's Upkeep, which runs before the rebuild of the sector records, pays the new owner the `cash_yield` of the last rebuild. When Control changes a sector's owner during resolution, that value still holds the Cash of sites whose progress was just reset. The rebuild clears `InfluencedBy` at the takeover and pays the new owner 1. |
| `income` | `0x04` | RULE-SITE-001 (writes), RULE-CHAOS-001, RULE-CHAOS-002, RULE-CONTROL-001, RULE-UI-011 | `MatchSectorState.Income` | same | |
| `tolerance` | `0x05` | RULE-SITE-001 (writes), RULE-CHAOS-002, RULE-AI-004, RULE-UI-011 | `MatchSectorState.Tolerance` | differs | Rebuilt before planning by `ToleranceResolver.RebuildBeforePlanning`, which counts a site only when `SiteControlRules.Controller` names a player. The headquarters site (definition 21, Resistance 0) is complete at progress 0 and adds its Tolerance for any owner or none. In a neutral headquarters sector (after a third Crackdown or an elimination) the original counts it and the rebuild does not. |
| `support` | `0x06` | RULE-SITE-001 (writes), RULE-CONTROL-001, RULE-UI-011 | `MatchSectorState.Support` | same | The headquarters site has Support 0, so the owner test above changes nothing here. |
| `sites` | `0x07` | see FMT-STATE-004 | `MatchSectorState.Sites` | same | Three slots in slot order. |
| `research_level` | `0x0D` | RULE-SITE-001 (writes), RULE-AI-005 and RULE-AI-026 (through `local_tech_cap`), SCR-RESEARCH-001 | `SpecialSiteRules.ResearchTechLimit`, computed from sites whose `InfluencedBy` is the gang's owner in a sector the gang's owner holds | differs | The original's `local_tech_cap` reads this byte whoever owns the sector (FND-AI-054). The rebuild lifts the cap only in the gang owner's own sector, so a computer gang standing in another player's sector with a completed Science Center or Research Lab keeps the cap of 5. For a human's own sector the value agrees. |
| `factory` | `0x0E` | RULE-SITE-001 (writes), RULE-EQUIP-003, RULE-FINANCE-001 | `SpecialSiteRules.EquipmentCost`, from sites whose `InfluencedBy` is the buyer | representation | RULE-EQUIP-003 also requires the buyer to own the sector. `InfluencedBy` is set at the same rebuild and cleared only when the owner changes, and then the owner test fails in both. |
| `crackdown_turns` | `0x0F` | RULE-SETUP-005, RULE-POLICE-001, RULE-POLICE-002, RULE-POLICE-003, RULE-CONTROL-001, RULE-TURN-004, RULE-AI-004, RULE-AI-006, RULE-AI-011, RULE-AI-013 | `MatchSectorState.CrackdownTurnsRemaining` | differs | `CrackdownResolver.FinishCombat` subtracts 1 from every positive value. RULE-POLICE-003 leaves a value of 100 or more alone, so the permanent police the island name rule sets (RULE-SETUP-005, `OriginalCityGenerator` writes 100) run out after 100 turns in the rebuild and never in the original. |
| `gangs_seen` | `0x10` | RULE-HIRE-003, RULE-UI-006, RULE-UI-010 | Computed on demand from `MatchState.CanPlayerDetectGang` (`SectorGangView.Visible` in `src/Rechaos.Game/UiNavigation.cs`); the hire drop test in `HireResolution` checks the player's own active gangs | representation | The player's own byte is set exactly when it has a living gang there, which the hire test reads directly. The other bytes follow `visible_to` (FMT-STATE-001). |
| `site_combat` to `site_martial_arts` | `0x16` to `0x23` | RULE-SITE-001 (writes), RULE-GANG-001 | Not stored; `EffectiveStatisticsCalculator` adds each completed site's modifiers into `MatchGangState.StoredStatistics` | representation | Only RULE-GANG-001 reads the sums. The headquarters site has no modifiers, so the owner test does not matter here. Per-sector sums stay within six either way (RULE-CITY-002), so the byte wrap is never reached. |

Verdict: stays `partial`. `cash_yield` (RULE-UPKEEP-001), `tolerance` (RULE-SITE-001,
RULE-CHAOS-002), `research_level` (RULE-AI-005, RULE-AI-026) and `crackdown_turns`
(RULE-POLICE-003, RULE-SETUP-005) differ, and no deviation covers them.

## FMT-STATE-003: Per-gang combat record

Detailed Combat (RULE-COMBAT-004) is the only reader of these records for presentation. The
computer players read two bytes of them through the sector index 64, which lies past the sector
list (RULE-AI-005, RULE-AI-013). The rebuild keeps no records; it derives the presentation from
the combat-phase `GameEvent`s.

| Field | Offset | Rules | Rebuild state | Match | Notes |
|---|---|---|---|---|---|
| `definition` | `0x00` | RULE-COMBAT-002 (writes), RULE-COMBAT-004 | `CombatantDetails.DefinitionId` on the attack or police event | same | Recorded when the gang fought. |
| `definition` of record 0, as owner at sector 64 | `0x00` | RULE-AI-005, RULE-AI-013 | Constant 0 in `AiPlanningPreparation.RefreshHireAnchor` and `OriginalAiEquipmentRules` | differs | Holds 0 while player 0's slot 0 is the Right Hands. Once a hire of another definition reuses that slot and the gang fights, the original reads its definition number. RULE-AI-005 calls the reading of byte 0 disputed. |
| `force_start` | `0x01` | RULE-COMBAT-002 (writes), RULE-COMBAT-004 | `CombatForceTimeline.InitialForce` (`src/Rechaos.Game`) | differs | Taken from `PreviousForce` or `PreviousValue` of an event that targets the gang. For a gang that only attacked, it is recovered as current Force plus the phase's damage, capped at 10. An attacker killed by a retaliation larger than its Force is drawn from too high a start. |
| `force_final` | `0x02` | RULE-COMBAT-002 (writes), RULE-COMBAT-004 | `MatchGangState.Force` after the phase | representation | Floored at 0 where the original may go below 0; the bar draws nothing below 0 in either. Not checked on screen. |
| `force_shown` | `0x03` | RULE-COMBAT-004 | `CombatForceTimeline.Forces` | same | Starts at the phase-start force and moves only by the clips shown, as the rule says. Inherits the `force_start` difference. |
| `damage_dealt` | `0x04` | RULE-COMBAT-002 (writes), RULE-COMBAT-004 | `CommandResolutionDetails.Damage`, with `CommandResolutionCode.TargetEvaded` for -1 | representation | The undefined value for a gang that did not attack has no counterpart; RULE-COMBAT-004 does not read it for such a gang. Not checked in detail. |
| `retaliation_taken` | `0x05` | RULE-COMBAT-002 (writes), RULE-COMBAT-004, RULE-AI-013 (record 1, as Crackdown at sector 64) | `CommandResolutionDetails.RetaliationDamage`; constant 0 for the AI read | representation | RULE-AI-013 reads the aliased byte only for a cell whose owner is -1, and the owner read at sector 64 is never -1, so the value cannot reach a result. |
| `weapon`, `armor`, `misc` | `0x06` | RULE-COMBAT-002 (writes), RULE-COMBAT-004 | `CombatantDetails.WeaponItemId`, `ArmorItemId`, `MiscellaneousItemId` | representation | null for -1. |
| `police_damage` | `0x09` | RULE-POLICE-001 (writes), RULE-COMBAT-004 | `PoliceAttackResolutionDetails.Detected` and `Damage` | representation | -1 becomes `Detected` false. |

Verdict: stays `partial`. The sector-64 read of `definition` differs (RULE-AI-005, RULE-AI-013),
and `force_start` differs for Detailed Combat (RULE-COMBAT-004).

## FMT-STATE-004: Site slot

| Field | Offset | Rules | Rebuild state | Match | Notes |
|---|---|---|---|---|---|
| `definition` | `0x00` | RULE-CITY-002, RULE-CITY-003, RULE-SITE-001, RULE-SEARCH-002, RULE-AI-004, RULE-AI-006, RULE-AI-022, RULE-AI-026, RULE-AI-028 | `MatchSiteState.DefinitionId` | same | |
| `progress` | `0x01` | RULE-INFLUENCE-001, RULE-SITE-001, RULE-CONTROL-001, RULE-POLICE-002, RULE-TURN-004, RULE-TURN-006, RULE-OBJECTIVE-003, RULE-SEARCH-002, RULE-AI-004 | `MatchSiteState.Resistance` (the Resistance still needed) | representation | `progress` = definition Resistance minus `Resistance`. Influence caps progress at the Resistance in both, so every test (complete, not complete, remaining at least 1) reads the same. A reset writes the definition's Resistance. |
| influencer (not in the original) | | none | `MatchSiteState.InfluencedBy` | representation | Set to the owner at the rebuild before planning for a site completed in the last turn, cleared when the sector changes hands. It equals "complete and counted since the last rebuild" except for the headquarters site, which it never names; the results of that are listed under `tolerance` and `cash_yield` in FMT-STATE-002. |

Verdict: every field is `same` or `representation`, so this row can be `complete`. The
differences that `InfluencedBy` causes belong to the FMT-STATE-002 fields and keep that row
`partial`.

## FMT-STATE-005: Comlink message record

| Field | Offset | Rules | Rebuild state | Match | Notes |
|---|---|---|---|---|---|
| `occupied` | `0x00` | RULE-COMLINK-001, RULE-COMLINK-004, RULE-COMLINK-005, RULE-COMLINK-007 | Membership in `ComlinkInbox.Messages` | differs | Same during play: 16 per player, the oldest dropped when full. The original does not save the inbox and empties it whenever a match is entered, so a loaded match starts with no messages (FND-COMLINK-006, FND-SEARCH-005). The rebuild saves and restores the inbox. DEV-SAVE-001 covers the save format only. |
| `read` | `0x01` | RULE-COMLINK-004, RULE-COMLINK-005, RULE-COMLINK-007 | `ComlinkInbox.IsRead` (`ReadSequences`) | representation | Per message; an empty record has no entry. |
| `turn` | `0x02` | RULE-COMLINK-003 (writes), RULE-COMLINK-005 | `ComlinkMessage.Turn` | representation | One-based; the View date subtracts 1 (`MatchDate` in `src/Rechaos.Game/ChaosGame.City.cs`). The 16-bit wrap is not reproduced; a match does not reach turn 32,768. |
| `sender` | `0x04` | RULE-COMLINK-003 (writes), RULE-COMLINK-005 | `ComlinkMessage.Sender` | same | |
| `text` | `0x05` | RULE-COMLINK-003, RULE-COMLINK-005, RULE-COMLINK-006 | `ComlinkMessage.Text` | not checked | Up to 160 characters. Whether trailing spaces are kept, and whether an all-space message can be sent as in the original, was not compared. |
| `unk_A5` | `0xA5` | none | none | representation | No rule reads it. |

Verdict: stays `partial`. `occupied` differs after a load (RULE-COMLINK-004), and `text` is not
checked (RULE-COMLINK-006).

## FMT-STATE-006: Last Turn report record

The rebuild keeps `MatchState` notifications (`GameNotification`) and events, and
`LastTurnEventProjection.Select` in `src/Rechaos.Game/ChaosGame.EventsPanel.cs` derives the reports of
the completed turn from them, keeping the first 32.

| Field | Offset | Rules | Rebuild state | Match | Notes |
|---|---|---|---|---|---|
| `occupied` | `0x00` | RULE-EVENT-001, RULE-EVENT-002, RULE-EVENT-005 | Presence in the derived list | differs | `Select` keeps one Influence report per sector and turn, while RULE-INFLUENCE-001 and RULE-EVENT-006 record one per completed site. Two sites completed in the same sector in one resolution give two reports in the original and one in the rebuild. The one-per-sector filter is right for Control. |
| `unk_01` | `0x01` | none | none | representation | Padding. |
| `report_type` | `0x02` | RULE-EVENT-002, RULE-EVENT-005, RULE-EVENT-004, RULE-EVENT-006 to RULE-EVENT-014 | `GameNotification.Kind`, with the related `GameEvent` for failed Bribe and Equip | representation | Filtered by `NotificationPresentation.IsLastTurnReport`. |
| `arg1` to `arg3` | `0x04` | RULE-EVENT-002, RULE-EVENT-005 and the recording rules | `GameNotification.SectorId`, `Gang`, and the related `GameEvent` (target, definition, player) | not checked | Each report type's arguments were not compared one by one; the recipients follow the RULE-EVENT rows. |

The counts `last_turn_report_count` are not part of the record and are not saved; the rebuild has
no count and no clearing step, since the list is derived.

Verdict: stays `partial`. `occupied` differs for two sites completed in one sector in one
resolution (RULE-EVENT-006, RULE-EVENT-005), and the arguments are not checked.

## FMT-STATE-007: Computer player planning record

The rebuild keeps this record as parallel arrays in `AiPlanningState`, 6 x 81 entries each,
indexed by `player * 81 + slot`. `NativeSaveSerializer` saves every array and the state
fingerprint includes them.

| Field | Offset | Rules | Rebuild state | Match | Notes |
|---|---|---|---|---|---|
| `family` | `0x00` | RULE-AI-001, RULE-AI-002, RULE-AI-006, RULE-AI-010, RULE-AI-019, RULE-AI-022, RULE-AI-025, RULE-AI-026, RULE-AI-031 | `AiPlanningState.Family` | same | The reset writes `UnusedFamily` (99). `SetRaiderFamily` writes 9 before the dispatch, as in RULE-AI-001. |
| `needs_family` | `0x01` | RULE-AI-001, RULE-AI-002, RULE-AI-003, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-025, RULE-AI-026, RULE-AI-030 | `AiPlanningState.NeedsFamily` (bool) | differs | Set for slot 0 on the first pass, for empty slots and by the Greed Terminate branch, and cleared by the reset, as in the original. `SetFamily` also clears it, so the RULE-AI-010 rewrite (`AiPlanningPreparation.RevertSurplusHunter`) clears a flag a family-6 or family-12 Greed Terminate set earlier in the same pass. Whether a rule result can show this was not checked. |
| `older_action`, `older_target`, `older_target_2` | `0x02` | RULE-AI-001, RULE-AI-002, RULE-AI-019, RULE-AI-020, RULE-AI-022, RULE-AI-023 | `AiPlanningState.OlderAction`, `OlderTarget` | same | Rolled from the previous triplet for active gangs only. |
| `previous_action`, `previous_target`, `previous_target_2` | `0x05` | RULE-AI-001, RULE-AI-004, RULE-AI-006, RULE-AI-019 to RULE-AI-026, RULE-AI-031 | `AiPlanningState.PreviousAction`, `PreviousTarget` | same | Includes the duplicate cleanup and the RULE-AI-026 write. The target bytes are unsigned in the rebuild; every value stored is below 128. |
| `planned_action`, `planned_target`, `planned_target_2` | `0x08` | RULE-AI-001, RULE-AI-002, RULE-AI-004, RULE-AI-010, RULE-AI-019, RULE-AI-022, RULE-AI-031 | `AiPlanningState.PlannedAction`, `PlannedTarget` | same | `GangAction` numbers equal the FMT-STATE-001 action numbers. The target bytes of each family handler rest on the RULE-AI-019 to RULE-AI-031 rows. |
| `unk_0B` | `0x0B` | none | none | representation | Padding. |
| `weapon_cooldown` | `0x0C` | RULE-AI-001, RULE-AI-002, RULE-AI-019 to RULE-AI-023, RULE-AI-025 to RULE-AI-027 | `AiPlanningState.WeaponCooldown` (short) | same | Set to 0 with no weapon or no active gang, otherwise lowered by 1 with a 16-bit wrap. |
| `armor_cooldown` | `0x0E` | RULE-AI-001, RULE-AI-002, RULE-AI-019 to RULE-AI-023, RULE-AI-025 to RULE-AI-028 | `AiPlanningState.ArmorCooldown` (short) | same | As `weapon_cooldown`. The value 2 that RULE-AI-028 writes was not checked. |

Verdict: stays below `complete`. `needs_family` differs after the RULE-AI-010 rewrite of a
family-6 or family-12 gang that took the Greed Terminate branch in the same pass. Every other field
is `same` or `representation`.

## FMT-STATE-008: Combat result row of one sector

The Combat Results panel and Detailed Combat read these rows; no rule reads them for a game
result. The rebuild groups the combat-phase `GameEvent`s by sector instead
(`CombatResultProjection` in `src/Rechaos.Game/ChaosGame.CombatResults.cs`,
`CombatPresentationOrder` in `src/Rechaos.Game/CombatPresentationOrder.cs`).

| Field | Offset | Rules | Rebuild state | Match | Notes |
|---|---|---|---|---|---|
| `players[p][k].gang` | `0x00` | RULE-COMBAT-002 (writes), RULE-COMBAT-004 | `GameEvent.Gang` of attack and police events, grouped by `CombatantDetails.SectorId`, ordered by roster slot | not checked | RULE-COMBAT-004 plays only the viewer's gangs that fought. `CombatPresentationOrder.Order` follows that order, then also plays every other event of the phase the viewer can see. Whether that matches the rule was not checked. |
| `players[p][k].target` | `0x02` | RULE-COMBAT-002 (writes), RULE-COMBAT-004 | `GameEvent.Target` of the attack event | representation | -1 when the gang did not attack: no attack event. |
| `police_hit` | `0x90` | RULE-POLICE-001 (writes), RULE-COMBAT-004 | `PoliceAttackResolutionDetails.Detected` | representation | |

Verdict: cannot be `complete` yet. The gang entries, which decide which fights Detailed Combat
plays (RULE-COMBAT-004), are not checked.

## FMT-STATE-009: Input event record

RULE-UI-014 is the only rule that reads this record, and it decides only how input reaches the
screen loops; no game result depends on it. The rebuild polls MonoGame keyboard and mouse state in
`src/Rechaos.Game` and has no record of this shape.

| Field | Offset | Rules | Rebuild state | Match | Notes |
|---|---|---|---|---|---|
| `type` | `0x00` | RULE-UI-014 | MonoGame input polling in `ChaosGame` | not checked | |
| `a` | `0x04` | RULE-UI-014 | as above | not checked | Menu group, character or surface slot. |
| `b` | `0x08` | RULE-UI-014 | as above | not checked | Menu item, virtual key or client x. |
| `c` | `0x0C` | RULE-UI-014 | as above | not checked | Client y. |

Verdict: stays `missing` until RULE-UI-014 is compared with the rebuild; the fields affect input
handling only.
