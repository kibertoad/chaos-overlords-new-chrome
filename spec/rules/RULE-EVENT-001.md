---
id: RULE-EVENT-001
title: The Last Turn reports are cleared just before each resolution
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

The Last Turn Events panel only ever shows what happened in the turn that has
just been carried out. The game throws away every player's reports just before
it carries out a turn's orders.

## When it runs

After `planning_phase`, immediately before `resolution` starts, whichever
function then carries out the turn. The second loop runs at the start of
`resolution`, before any report is recorded [FND-EVENT-004].

## Parameters

None.

## Inputs

`last_turn_reports`, `last_turn_report_count`, `turn_order`.

## Procedure

```text
for each player in turn_order:
    for i in 0..32:
        last_turn_reports[player * 32 + i].occupied = 0
# resolution starts here
for each player in turn_order:
    last_turn_report_count[player] = 0
```

## Outputs

No return value. Clears the `occupied` byte of all 192 records and sets all
six counts to 0. The other bytes of each record (FMT-STATE-006) keep their old
values.

## Edge cases

Reports a player never looked at are lost when the next resolution starts.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Event), says the Last Turn Events panel
shows what happened during the player's last turn and opens by itself at the
start of the turn when something did. It does not say when the reports are
discarded.

## Differences between builds

None known.

## Open questions

None known. The clearing loop visits players 0 to 5 and each player's records
0 to 31 [FND-EVENT-004]; the order does not change the result.
