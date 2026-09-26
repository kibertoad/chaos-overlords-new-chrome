---
id: RULE-TURN-004
title: At turn start, recurring actions that can no longer apply are cleared and the rest become the gangs' actions
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-004, FND-TURN-006, FND-TURN-009, FND-HIDE-001, FND-TURN-002, FND-RESEARCH-001, FND-PLATFORM-003, FND-COMBAT-004, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002, FMT-STATE-004, FMT-DATA-001]
---

## Summary

At the start of every turn each gang's order is replaced by its recurring
order, or by nothing when it has none. A recurring order is dropped first when
it has done its job or can no longer be carried out: Control once the sector is
taken or the police are in it, Heal once the gang is at full Force, Influence
once the site is won or the sector lost, Research once the item is researched.
Dead gangs lose their recurring orders.

## When it runs

First in `turn_start`, before Upkeep, from the second pass of the turn loop on
(RULE-TURN-001). The first pass, of a new match or of a loaded one, skips it
(FND-TURN-006).

## Parameters

None.

## Inputs

`turn_order`, `gangs` (each gang's `sector`, `force`, `repeat_action` and
`repeat_target`), `sectors` (each sector's `owner`, `crackdown_turns` and
`sites`), `site_definitions`, `research_remaining`.

## Procedure

```text
for each player in turn_order:
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.repeat_action == ACTION_CONTROL:
            let here = sectors[gang.sector]
            if here.owner == player or here.crackdown_turns > 0:
                gang.repeat_action = ACTION_NONE
        else if gang.repeat_action == ACTION_HEAL:
            if gang.force == 10:
                gang.repeat_action = ACTION_NONE
        else if gang.repeat_action == ACTION_INFLUENCE:
            let here = sectors[gang.sector]
            let target_site = here.sites[gang.repeat_target]
            if target_site.progress == site_definitions[target_site.definition].resistance or here.owner != player:
                gang.repeat_action = ACTION_NONE
        else if gang.repeat_action == ACTION_RESEARCH:
            if research_remaining[gang.repeat_target * 6 + player] == 0:
                gang.repeat_action = ACTION_NONE
        if gang.sector == GANG_INACTIVE:
            gang.repeat_action = ACTION_NONE
        gang.action = gang.repeat_action
        gang.target = gang.repeat_target
```

## Outputs

No return value. Changes `repeat_action` of the gangs whose recurring action is
cleared, and sets every gang's `action` and `target` from its `repeat_action`
and `repeat_target`. Makes no draws.

## Edge cases

Chaos and Hide have no test and repeat until the player replaces or cancels
them or the gang dies. Bribe and Snitch have no test either; no human order
stores them as recurring (RULE-TURN-005).

A one-off order of any kind ends here: its `repeat_action` is `ACTION_NONE`, so
the gang starts the turn with no action. A one-off Hide therefore ends at this
point and the gang stops hiding (RULE-HIDE-001).

A recurring Control that failed because police arrived is dropped even when the
Crackdown is about to end. A recurring Influence dropped because the sector was
lost does not come back if the sector is won again.

For an inactive gang (`sector` 100) with a recurring Control or Influence, the
tests read `sectors[100]`, past the end of the 64 sector records. At the
addresses the glossary gives, that record falls inside `combat_records`, so
the original reads bytes of the last combat phase's records there. The result is thrown away, since the inactive test
clears the action either way.

`repeat_target` is copied into `target` even when the recurring action has been
cleared.

`repeat_target` is the site slot, 0 to 2, of a recurring Influence and the item
record number of a recurring Research: the pickers write those values and the
tests read them the same way (FND-TURN-006, FND-TURN-009).

This rule never touches `crackdown_turns`; the only countdown of police
presence is RULE-POLICE-003, at the end of `resolution` (FND-TURN-006).

## What the sources say

SRC-MANUAL-GOG, numbered page 28, says a continuous command is given with the
double green arrow and says nothing of when it stops. The executable ends it
at the turn start after it can no longer apply, for the four actions above.

## Differences between builds

None known.

## Open questions

- No run of the original has confirmed when a recurring order is dropped.
