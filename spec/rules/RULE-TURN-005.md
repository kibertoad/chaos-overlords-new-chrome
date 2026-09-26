---
id: RULE-TURN-005
title: Giving a gang an order replaces its whole previous order, one-off or recurring
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-002, FND-TURN-009, FND-HIDE-001, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

A human player gives an order to one gang, or to all of a sector's gangs at
once through the sector's command bar. Either way the new order replaces the
old one completely. A one-off order also cancels any recurring order. A
recurring order takes effect this turn and is renewed at the start of each
later turn. Only Chaos, Control, Heal, Hide, Influence and Research can be made
recurring, and Research only for a single gang.

## When it runs

During `planning_phase`, each time a human player confirms an order in a
gang's command menu or in a sector's command bar (RULE-TURN-001).

## Parameters

- `chosen: FMT-STATE-001[]`: the gang given the order, or, for a command-bar
  order, every one of the player's gang records whose `sector` is the selected
  sector, in roster slot order; for Heal, only those whose `force` is below 10.
- `new_action`: the chosen action, a value of `action`.
- `new_target`, `new_target_2`: the targets the action's picker produced; 0
  when the action has no picker. Influence gives a site slot, 0 to 2, and
  Research an item record number.
- `recurring`: true when the order was given as a recurring one.
- `sector_wide`: true for an order from a sector's command bar.

## Inputs

None beyond the parameters.

## Procedure

```text
for each gang in chosen:
    if sector_wide:
        if recurring:
            gang.repeat_action = new_action
        else:
            gang.repeat_action = ACTION_NONE
        gang.repeat_target = new_target
        gang.action = new_action
        gang.target = new_target
        gang.target_2 = new_target_2
    else if recurring:
        if new_action == ACTION_INFLUENCE or new_action == ACTION_RESEARCH:
            gang.target = new_target
            gang.repeat_target = new_target
        gang.repeat_action = new_action
        gang.action = new_action
    else:
        # the picker has already written target (and target_2)
        gang.repeat_action = ACTION_NONE
        gang.repeat_target = 0
        gang.action = new_action
```

## Outputs

No return value. Sets `action`, `repeat_action` and, as above, the target
bytes of each chosen gang. Makes no draws.

## Edge cases

Choosing recurring None clears both the recurring and the active action, which
cancels the order. Giving any order to a hiding gang, or cancelling its Hide,
changes its active action at once, so it stops hiding at once (RULE-HIDE-001).
Giving Hide makes the gang hidden at once, during planning.

The recurring choices offered are:

| Menu | Actions |
|---|---|
| A gang's recurring submenu | Chaos, Control, Heal, Hide, Influence, Research, None |
| A sector's recurring submenu | Chaos, Control, Heal, Hide, Influence, None |

A gang's recurring Chaos, Control, Heal or Hide leaves `target` and
`repeat_target` as they were. A command-bar order writes its target into
`repeat_target` even when it is one-off, which has no effect while
`repeat_action` is 0.

A command-bar order reaches hiding gangs like any other, and it also passes
over the player's roster slot 80, which the game uses as scratch space while
the order is chosen; the turn-start cleanup clears what it writes there
(FND-TURN-009).

A gang can also be given a recurring Influence without the menu: pointing at
an unfinished site of a sector the player owns, through the gang command's
second input path, writes Influence into `action` and `repeat_action` and the
site slot into `target` and `repeat_target` (FND-TURN-009).

Bribe and Snitch are never offered as recurring. If a recurring value of 2 or
13 were stored some other way, the turn-start cleanup would keep it forever
(RULE-TURN-004).

## What the sources say

SRC-MANUAL-GOG, numbered page 28, says that with two or more gangs in a sector a
command bar appears over the top two, that a command chosen there is given to
all the player's gangs in the sector, replacing the orders they had, and that
continuous commands are given through the double green arrow. It does not list
which commands can be continuous.

## Differences between builds

None known.

## Open questions

- How the player starts the gang command's second input path (a drag of the
  gang is the likely reading) has not been read (FND-TURN-009).
- The computer players' orders are written by their own code, which this rule
  does not cover.
