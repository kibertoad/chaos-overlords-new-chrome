---
id: RULE-AI-010
title: A computer player picks a hire role from its scenario's turn schedule, then hires, places or snubs
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-003, FND-AI-009, FND-AI-013, FND-AI-014, FND-AI-017, FND-AI-008, FND-AI-011]
conflicting: []
split_with: []
related: [RULE-AI-008, RULE-AI-009, RULE-AI-011, RULE-AI-012, RULE-AI-004, FMT-STATE-001]
---

## Summary

After planning its gangs' orders, a computer player decides whom to hire. Its
scenario gives it a fixed cycle of ten hire roles (eleven in Dominance), one
per turn; the role decides how it ranks the offers and which family the new
gang joins. It then hires the best offer into its placement sector, or snubs
an offer when it cannot or will not hire.

## When it runs

At the end of RULE-AI-001, once per computer player per turn, after every gang
has been dispatched.

## Parameters

`player`: the computer player planning.

## Inputs

`scenario`, `elapsed_turns`, `hire_role`, `previous_hire_role`,
`placement_anchor`, `sector_weight`, `gangs` (the Right Hands' `sector`), and
what RULE-AI-008, RULE-AI-009, RULE-AI-011 and RULE-AI-012 read.

## Procedure

```text
if not hire_allowed(player):
    return
previous_hire_role[player] = hire_role[player]
# eleven cells per scenario, schedule slots 0 to 10; -1 where a scenario has no slot 10
let modes = [
    0, 4, 0, 2, 0, 3, 2, 0, 2, 3, -1,
    0, 1, 0, 0, 4, 0, 3, 0, 3, 2, -1,
    0, 4, 3, 0, 2, 3, 2, 0, 2, 0, -1,
    0, 0, 2, 2, 4, 3, 2, 0, 2, 0, 3,
    0, 1, 0, 0, 4, 0, 3, 0, 3, 2, -1,
    0, 1, 0, 0, 4, 0, 3, 0, 3, 2, -1,
    0, 1, 0, 1, 4, 1, 1, 0, 1, 2, -1,
    0, 4, 0, 2, 0, 3, 2, 3, 2, 5, -1,
    0, 1, 0, 1, 1, 2, 1, 0, 1, 1, -1,
    0, 1, 0, 3, 0, 3, 0, 3, 0, 2, -1]
let roles = [
    1, 6, 1, 2, 1, 4, 2, 1, 2, 3, -1,
    0, 1, 0, 0, 6, 0, 4, 0, 3, 5, -1,
    1, 6, 4, 1, 2, 3, 2, 1, 2, 1, -1,
    1, 1, 5, 2, 6, 3, 2, 1, 5, 1, 4,
    0, 1, 0, 0, 6, 0, 4, 0, 3, 5, -1,
    0, 1, 0, 0, 6, 0, 4, 0, 3, 5, -1,
    0, 2, 0, 2, 6, 1, 2, 0, 1, 5, -1,
    1, 6, 1, 2, 1, 4, 2, 4, 2, 3, -1,
    0, 2, 0, 1, 1, 3, 2, 0, 1, 2, -1,
    0, 1, 0, 3, 0, 4, 0, 3, 0, 5, -1]
let period = 10
if scenario == 3:
    period = 11
let slot = elapsed_turns % period
# the scenario's adjustments to slot run here; see Open questions
let mode = modes[scenario * 11 + slot]
let role = roles[scenario * 11 + slot]
hire_role[player] = role
let offer = rank_offer(player, mode)
if offer == -1:
    let k = offer_to_snub(player)
    if k >= 0:
        hire_orders[player * 3 + k] = -2
    return
let place = placement_anchor[player]
if role == 4 and (scenario <= 5 or scenario == 9):
    # the first sector, in ascending order, holding a visible hostile human gang
    let hostile = 100
    for s in 0..64:
        if sector_weight[player * 64 + s] == 10:
            hostile = s
            break
    if hostile != 100:
        place = hostile + 0x40
if scenario == 7 and (slot == 5 or slot == 7):
    place = gangs[player * 81].sector + 0x40
hire_destination(player, place, offer)
```

## Outputs

No return value. Sets `previous_hire_role[player]` to the role before this
turn's and `hire_role[player]` to the new role. Either stores a placement
sector in the chosen offer's element of `hire_orders` (the hire itself is
carried out in `hire_phase`), or stores -2 (snub) in one offer's element, or
leaves `hire_orders` unchanged. Makes no draw: the placement is always encoded
(RULE-AI-012).

## Edge cases

The first turn uses schedule slot 0, since `elapsed_turns` starts at 0.
Scenarios 1, 4 and 5 share one schedule. Siege's slots 5 and 7 place the new
gang with the Right Hands; the family-6 role in the other scenarios places it
in the first sector where a hostile human gang is visible.

## What the sources say

SRC-MANUAL-GOG does not describe how computer players hire.

## Differences between builds

None known.

## Open questions

- The scenario adjustments to the schedule slot are not written out. For
  scenarios 1, 4 and 5 they are known in outline (late turns remap slots 4 and
  8; slot 6 is redirected by the first visible hostile sector, family-6
  coverage, the previous hire role and the presence of families 5 and 7;
  existing family counts cap slots 8, 6, 9 and 4 at four, four, three and one
  times the match length over 52; fewer than four gangs of family 0 or 4 force
  slot 0), and for the other scenarios only their shape is recorded
  (FND-AI-009). In Greed, slot 9 becomes slot 0 for a player with a nonzero
  `scenario_standing` when fewer than ten turns remain or cash is below 100.
  The family-6 guards compare `previous_hire_role` with slot numbers
  (BUG-AI-001).
- Whether `previous_hire_role` is updated before or after the gate is not
  recorded.
- When the ranking fails and `offer_to_snub` returns -1 (every value 5000 or
  more), nothing is snubbed here; that is assumed.
- Whether the visible-hostile override is skipped when no sector has weight 10
  (the query returns 100) is assumed; FND-AI-017 leaves it open.
- The override for Siege is tied to schedule slots 5 and 7; after adjustments
  the slot may differ from the base slot, and which one the override tests is
  not recorded.
- The scenario numbering of 6 (Eliminate) and 7 (Siege) follows FND-AI-002 and
  is contested by FND-UI-033 and FND-TURN-003.
