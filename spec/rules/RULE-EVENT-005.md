---
id: RULE-EVENT-005
title: The Last Turn Events panel shows the viewer's recorded reports in the order they were recorded
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-002, FND-EVENT-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-EVENT-001]
---

## Summary

The panel pages through the active player's reports one at a time, first
recorded first. Each report shows a status line chosen by its type and an
illustration.

## When it runs

When the Last Turn Events panel opens, and again for each report it shows.

## Parameters

None.

## Inputs

`active_player`, `last_turn_reports`.

## Procedure

```text
let shown: INT32[] = []
for i in 0..32:
    if last_turn_reports[active_player * 32 + i].occupied:
        append(shown, i)
show SCR-EVENT-001
```

## Outputs

No return value. Opens SCR-EVENT-001 on the reports in `shown`, in ascending
record order, which is the order they were recorded (RULE-EVENT-002). Page
`p` shows the record `shown[p]`. A report of type 4 draws the completed site's
picture and one of type 5 the researched item; a report of any other type `t`
draws the illustration with resource number `6000 + t`, so every cash failure
(type 6) draws `PX06006` whichever order failed.

## Edge cases

- Reports from an older resolution are never shown, because RULE-EVENT-001
  clears them first.
- With no occupied record the list is empty; whether the panel still opens is
  not recorded.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Event), says the panel opens by itself at
the start of the player's turn when something notable happened, does not open
when nothing did, and that closing it before viewing every event makes a
yellow light blink on the Events button. The executable evidence here covers
neither the automatic opening nor the blinking light.

## Differences between builds

None known.

## Open questions

- What the panel does when the list is empty, and whether it plays the
  rejected-input sound then, is not recorded.
- The automatic opening at the start of a turn and the blinking Events button
  are described only by the manual.
- Which string resource from 33 to 44 each type (and each cash-failure
  argument) shows is not recorded.
