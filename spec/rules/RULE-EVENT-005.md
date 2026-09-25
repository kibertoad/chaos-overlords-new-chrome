---
id: RULE-EVENT-005
title: The Last Turn Events panel shows the viewer's recorded reports in the order they were recorded
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-002, FND-EVENT-003, FND-EVENT-005, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-EVENT-001, FMT-STATE-006]
---

## Summary

The panel pages through the active player's reports one at a time, first
recorded first. It opens by itself at the start of a player's planning when
the last turn left that player at least one report, and the Events light stays
on until every report has been shown once. With no reports the panel does not
open.

## When it runs

`events_planning_start` runs once at the start of each human player's planning
visit, after the combat presentation. `events_show` runs from there and when
the player presses the Events control. `events_page_drawn` runs each time the
panel draws a page: when it opens and after each step.

## Parameters

None.

## Inputs

`active_player`, `last_turn_reports`, `events_page`, `events_seen`,
`events_unviewed`.

## Procedure

```text
define events_show() -> UINT8:
    let count = 0
    for i in 0..32:
        if last_turn_reports[active_player * 32 + i].occupied:
            count = count + 1
    if count == 0:
        return 0
    show SCR-EVENT-001
    return 1
define events_planning_start():
    events_page = 0
    if last_turn_reports[active_player * 32].occupied:
        for i in 0..32:
            events_seen[i] = last_turn_reports[active_player * 32 + i].occupied == 0
        events_unviewed = 1
        events_show()
    return
define events_page_drawn():
    events_seen[events_page] = 1
    events_unviewed = 0
    for i in 0..32:
        if events_seen[i] == 0:
            events_unviewed = 1
    return
```

## Outputs

`events_show` returns 1 when it opened SCR-EVENT-001 and 0 when the player
has no report; then the panel stays closed and the rejected-input sound plays
(SCR-EVENT-001, Sounds). Page `p` shows record `p`: the recorder fills records
from 0 without gaps (RULE-EVENT-002), so the pages are the reports in the
order they were recorded. The panel keeps `events_page` when it closes, so
the Events control reopens it at the page shown last, until the next planning
visit starts again at page 0. `events_unviewed` is the Events light
(SCR-EVENT-001 describes what each page draws).

## Edge cases

- Reports from an older resolution are never shown, because RULE-EVENT-001
  clears them first.
- The panel opens by itself only when record 0 is occupied, which is the same
  as the player having any report.
- `events_seen` starts at 1 for every unoccupied record, so the light goes out
  once each report has been drawn once, in any order.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Event), says the panel opens by itself at
the start of the player's turn when something notable happened, does not open
when nothing did, and that closing it before viewing every event makes a
yellow light blink on the Events button. The executable agrees.

## Differences between builds

None known.

## Open questions

- Whether the light blinks or stays lit depends on the event pump's phases,
  which FND-EVENT-005 does not fully trace.
