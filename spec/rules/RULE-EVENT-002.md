---
id: RULE-EVENT-002
title: Recording a Last Turn report keeps the first 32 reports of a resolution
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

Each notable event of a turn adds a report for the player it concerns. A
player can have at most 32 reports from one turn; any later report that turn
is dropped.

## When it runs

During `resolution`, at the point where another rule detects one of the
events listed under Parameters. All 12 places that record a report are inside
the whole-turn resolver.

## Parameters

- `player`: the player slot that receives the report.
- `report_type`: the kind of report, from this list:

| Value | Recorded when | Recipients | Recorded through |
|---|---|---|---|
| 0 | Never recorded; the compositor's empty case | None | None |
| 1 | A Crackdown is created in a sector | Each player with a gang in the sector when resolution began | RULE-EVENT-004 |
| 2 | A player takes control of a sector | The new owner | Not yet written |
| 3 | A player loses control of a sector | The previous owner | Not yet written |
| 4 | An Influence completes a site | The influencing player | RULE-EVENT-006 |
| 5 | Research completes an item | The researching player | RULE-EVENT-007 |
| 6 | Bribe, Equip or Hire fails for lack of cash; an argument of 1, 2 or 4 tells which | The player whose order failed | RULE-EVENT-008 (Bribe), RULE-EVENT-009 (Hire); Equip not yet written |
| 7 | A Hire fails because the sector is full | The hiring player | RULE-EVENT-010 |
| 8 | A Hire fails because the player has the most gangs allowed | The hiring player | RULE-EVENT-011 |
| 9 | A player is eliminated | All six player slots | RULE-EVENT-003 |

## Inputs

`last_turn_reports`, `last_turn_report_count`.

## Procedure

```text
let n = last_turn_report_count[player]
if n == 32:
    return
let report = last_turn_reports[player * 32 + n]
report.occupied = 1
report.report_type = report_type
# the three arguments the caller passes are stored here too (see Open questions)
last_turn_report_count[player] = n + 1
```

## Outputs

No return value. Fills the next free record of `player` and adds 1 to the
player's count, or changes nothing when the player already has 32 reports.

## Edge cases

- The 33rd and later reports of a player in one resolution are dropped. No
  earlier report is moved or overwritten.
- No other kind of event reaches the panel: a refused order such as a failed
  move, an evaded target or an item that cannot be bought records nothing.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Event), lists taking over a sector, fully
influencing a site, completing research and a Crackdown as examples of what
the panel shows. It gives no limit on the number of reports, and it does not
mention the cash-failure, hire-failure or elimination reports.

## Differences between builds

None known.

## Open questions

- The offsets of `occupied`, `report_type` and the three arguments inside the
  10-byte record are not recorded, and the record's format entry is not
  written.
- Which arguments each of the 12 callers passes, and which argument carries
  the 1, 2 or 4 of a cash failure, are not recorded. The rules that detect
  each event (sector control, Influence, Research, Bribe, Equip and Hire) call
  this rule at a point their own findings have to give.
- Whether the recorder sets `occupied` to 1 or to another nonzero value is not
  recorded.
