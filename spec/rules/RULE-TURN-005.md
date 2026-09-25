---
id: RULE-TURN-005
title: Giving a gang an order replaces its whole previous order, one-off or recurring
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-002, FND-HIDE-001, SRC-MANUAL-GOG]
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

- `chosen: FMT-STATE-001[]`: the gang given the order, or the sector's gangs a
  command-bar order applies to.
- `new_action`: the chosen action, a value of `action`.
- `new_target`: the target the action's picker produced.
- `recurring`: true when the order was given as a recurring one.
- `sector_wide`: true for an order from a sector's command bar.

## Inputs

None beyond the parameters.

## Procedure

```text
for each gang in chosen:
    if recurring:
        gang.repeat_action = new_action
        if not sector_wide:
            gang.repeat_target = new_target
        gang.action = new_action
    else:
        gang.action = new_action
        gang.target = new_target
        gang.repeat_action = ACTION_NONE
        if not sector_wide:
            gang.repeat_target = 0
```

## Outputs

No return value. Sets `action`, and `target` or `repeat_target`, and
`repeat_action` of each chosen gang. Makes no draws.

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

- Which of the sector's gangs a command-bar order applies to has not been
  recorded.
- Whether a recurring order from a gang's menu also writes `target`, and
  whether a command-bar order writes `repeat_target`, has not been recorded;
  the procedure writes only what the findings show.
- The gang menu's one-off path has a separate shortcut that gives a recurring
  Influence. What it is and how the player reaches it has not been recorded.
- The computer players' orders are written by their own code, which this rule
  does not cover.
