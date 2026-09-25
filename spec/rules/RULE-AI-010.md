---
id: RULE-AI-010
title: A computer player picks a hire role from its scenario's turn schedule, then hires, places or snubs
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-050, FND-AI-003, FND-AI-009, FND-AI-013, FND-AI-014, FND-AI-017, FND-AI-008, FND-AI-011, FND-AI-042, FND-AI-044, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AI-001, RULE-AI-008, RULE-AI-009, RULE-AI-011, RULE-AI-012, RULE-AI-004, BUG-AI-001, FMT-STATE-001]
---

## Summary

After planning its gangs' orders, a computer player decides whom to hire. Its
scenario gives it a fixed cycle of ten hire roles (eleven in Dominance), one
per turn; the role decides how it ranks the offers and which family the new
gang joins. Each scenario adjusts the turn's slot: late in the match some
slots fall back to the default hire, a hunter (family 6) is forced when a
hostile human gang is visible where no hunter covers it, each family is capped
at a multiple of the match length, and four or five gangs of family 0 or 4 come
before anything else. The player then hires the best offer into its placement
sector, or snubs an offer. Last, a player with more hunters than a quarter of
its sectors turns one back into family 0.

## When it runs

At the end of RULE-AI-001, once per computer player per turn, after every gang
has been dispatched. `previous_hire_role` has already been set to `hire_role`
at the start of that pass.

## Parameters

`player`: the computer player planning.

## Inputs

`scenario`, `elapsed_turns`, `turn_limit`, `cash`, `scenario_standing`,
`hire_role`, `previous_hire_role`, `placement_anchor`, `sector_weight`,
`aux_records` (`coverage_sector`), `planning_records` (`family`,
`planned_action`), `gangs` (`sector`), and what RULE-AI-008, RULE-AI-009,
RULE-AI-011 and RULE-AI-012 read.

## Procedure

`f` is a floating-point value, and every comparison with it converts the count
or the cash to floating point.

```text
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
# the slot the hunter test forces in each scenario; -1 where there is none
let hunter_slots = [5, 6, 2, 10, 6, 6, -1, -1, -1, 5]

define count(player, a, b):
    # active gangs of the player whose family is a or b
    let n = 0
    for slot in 0..81:
        let idx = player * 81 + slot
        let fam = planning_records[idx].family
        if gangs[idx].sector != GANG_INACTIVE and (fam == a or fam == b):
            n = n + 1
    return n

define first_hostile(player):
    for s in 0..64:
        if sector_weight[player * 64 + s] == 10:
            return s
    return 100

define covered(player, s):
    for slot in 0..81:
        let idx = player * 81 + slot
        if gangs[idx].sector != GANG_INACTIVE and planning_records[idx].family == 6
           and aux_records[idx].coverage_sector == s:
            return true
    return false

define hunter(player, k, F, G, missing, redirect):
    let h = first_hostile(player)
    if h == 100 or covered(player, h) or missing or previous_hire_role[player] == G:
        if k == F:
            return redirect
        return k
    return F

define schedule_slot(player):
    let f = turn_limit / 52.0
    let r = turns_remaining()
    let c = cash[player]
    let c2 = count(player, 2, 2)
    let c3 = count(player, 3, 3)
    let c5 = count(player, 5, 5)
    let c7 = count(player, 7, 7)
    let c6 = count(player, 6, 12)
    let c04 = count(player, 0, 4)
    let k = elapsed_turns % 10
    if scenario == 3:
        k = elapsed_turns % 11
    if scenario == 0:
        if r < 10 and (k == 1 or k == 3 or k == 6 or k == 8):
            k = 0
        if k == 9 and scenario_standing[player] != 0 and (r < 10 or c < 100):
            k = 0
        let red = 9
        if c7 == 0:
            red = 1
        else if c3 == 0:
            red = 3
        k = hunter(player, k, 5, 5, c3 < 1 or c7 < 1, red)
        if (k == 3 or k == 6 or k == 8) and c3 >= f * 4.0:
            k = 0
        if k == 5 and c6 >= f * 2.0:
            k = 0
        if k == 9 and (c2 >= f * 2.0 or c < f * 100.0):
            k = 0
        if k == 1 and c7 >= f:
            k = 0
        if c04 < 5:
            k = 0
    else if scenario == 1 or scenario == 4 or scenario == 5:
        if scenario == 1 and r < 10 and k == 4:
            k = 1
        if scenario == 1 and k == 8 and (r < 10 or c < 100):
            k = 1
        if scenario != 1 and k == 8 and c < 100:
            k = 1
        let red = 8
        if c7 == 0:
            red = 4
        else if c5 == 0:
            red = 9
        k = hunter(player, k, 6, 6, c5 < 1 or c7 < 1, red)
        if k == 8 and c2 >= f * 4.0:
            k = 0
        if k == 6 and c6 >= f * 4.0:
            k = 0
        if k == 9 and c5 >= f * 3.0:
            k = 0
        if k == 4 and c7 >= f:
            k = 0
        if c04 < 4:
            k = 0
    else if scenario == 2:
        if r < 5 and (k == 1 or k == 4 or k == 6 or k == 8):
            k = 0
        if k == 5 and (r < 10 or c < 100):
            k = 0
        let red = 5
        if c7 == 0:
            red = 1
        else if c5 == 0:
            red = 4
        k = hunter(player, k, 2, 2, c5 < 1 or c7 < 1, red)
        if (k == 4 or k == 6 or k == 8) and c5 >= f * 6.0:
            k = 0
        if k == 5 and c2 >= f * 2.0:
            k = 0
        if k == 2 and c6 >= f * 3.0:
            k = 0
        if k == 1 and c7 >= f:
            k = 0
        if c04 < 4:
            k = 0
    else if scenario == 3:
        if r < 8 and (k == 2 or k == 3 or k == 4 or k == 6 or k == 8):
            k = 0
        if k == 5 and (r < 10 or c < 100):
            k = 0
        let red = 5
        if c7 == 0:
            red = 4
        else if c3 == 0:
            red = 2
        else if c5 == 0:
            red = 3
        k = hunter(player, k, 10, 10, c3 < 1 or c7 < 1, red)
        if (k == 3 or k == 6) and c5 >= f * 3.0:
            k = 0
        if (k == 2 or k == 8) and c3 >= f * 3.0:
            k = 0
        if k == 5 and c2 >= f * 2.0:
            k = 0
        if k == 10 and c6 >= f * 3.0:
            k = 0
        if k == 4 and c7 >= f:
            k = 0
        if c04 < 4:
            k = 0
    else if scenario == 6:
        if k == 9 and c5 >= f * 3.0:
            k = 0
        if k == 4 and c7 >= f:
            k = 0
        if c04 < 4:
            k = 0
    else if scenario == 7:
        if (k == 3 or k == 6 or k == 8) and c3 >= f * 4.0:
            k = 0
        if (k == 5 or k == 7) and c6 >= f * 10.0:
            k = 0
        if k == 9 and (c2 >= f * 2.0 or c < f * 100.0):
            k = 0
        if k == 1 and c7 >= f:
            k = 0
        if c04 < 5:
            k = 0
        if c6 < 1:
            k = 5
    else if scenario == 8:
        if (k == 0 or k == 2 or k == 5 or k == 7) and c04 > 5:
            k = 4
    else if scenario == 9:
        let red = 3
        if c3 == 0:
            red = 9
        k = hunter(player, k, 5, 5, c3 < 1, red)
        if k == 3 and c2 >= f * 4.0:
            k = 0
        if k == 5 and c6 >= f * 4.0:
            k = 0
        if k == 9 and c3 >= f * 3.0:
            k = 0
        if c04 < 4:
            k = 0
        if c2 < 1:
            k = 3
    return k

if hire_allowed(player):
    let k = schedule_slot(player)
    let mode = modes[scenario * 11 + k]
    hire_role[player] = roles[scenario * 11 + k]
    let offer = rank_offer(player, mode)
    if offer == -1:
        let j = offer_to_snub(player)
        if j >= 0:
            hire_orders[player * 3 + j] = -2
    else:
        let place = placement_anchor[player]
        if k == hunter_slots[scenario]:
            place = first_hostile(player) + 0x40
        if scenario == 7 and (k == 5 or k == 7):
            place = gangs[player * 81].sector + 0x40
        hire_destination(player, place, offer)
# every scenario, whether or not the player may hire
let n = owned_sector_count(player)
if n / 4 < count(player, 6, 12) and n > 6:
    for slot in 0..81:
        let idx = player * 81 + slot
        let fam = planning_records[idx].family
        if gangs[idx].sector != GANG_INACTIVE and (fam == 6 or fam == 12):
            if planning_records[player * 81 + n].planned_action != ACTION_ATTACK:
                planning_records[idx].family = 0
                break
```

## Outputs

No return value. Sets `hire_role[player]` to the new role when the player may
hire. Either stores a placement sector in the chosen offer's element of
`hire_orders` (the hire itself is carried out in `hire_phase`), or stores -2
(snub) in one offer's element, or leaves `hire_orders` unchanged. May set one
family-6 or family-12 gang's family to 0. Makes no draw: the placement is
always encoded (RULE-AI-012).

## Edge cases

The first turn uses schedule slot 0, since `elapsed_turns` starts at 0.
Scenarios 1, 4 and 5 share the schedule, the hunter test and the quotas;
scenario 1 alone has the late-turn remaps of slots 4 and 8, and in scenarios 4
and 5 slot 8 falls back on cash alone. The quotas scale with the match length:
in a 52-turn match `f` is 1, so slot 1 of Greed is refused once one family-7
gang exists.

The hunter guard compares `previous_hire_role` with the hunter slot number
(BUG-AI-001). In Greed (5) and Dominance (10) it never fires; in scenarios 1, 4
and 5 it fires after a role-6 hire, in Acceptance after a role-2 hire and in
scenario 9 after a role-5 hire. When it fires the hunter slot is not forced,
and a turn whose own slot is the hunter slot takes the redirect.

A final slot equal to the hunter slot always comes from the forcing branch,
since the later adjustments set the slot only to 0 or 3, so `first_hostile`
gives a sector there. Siege's slots 5 and 7 place the new gang with the Right
Hands. Scenarios 0 and 7 test the offer's cost against cash again after the
ranking; the ranking has already made that test (RULE-AI-008), so it changes
nothing.

In the hunter reversion, the planned-action test reads the planning record of
roster slot `n` (the sector count), whichever hunter is being visited, so an
Attack planned by that unrelated gang spares every hunter that turn, and any
other action lets the first hunter in slot order go back to family 0. At most
one gang reverts per turn.

## What the sources say

SRC-MANUAL-GOG does not describe how computer players hire.

## Differences between builds

None known.

## Open questions

- When the ranking fails and `offer_to_snub` returns -1 (every value 5000 or
  more), nothing is snubbed here; that is assumed.
- The scenario numbering of 6 (Eliminate) and 7 (Siege) follows FND-AI-002 and
  is contested by FND-UI-033 and FND-TURN-003.
