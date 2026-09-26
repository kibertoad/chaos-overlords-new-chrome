---
id: RULE-EVENT-002
title: Recording a Last Turn report keeps the first 32 reports of a resolution
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-006]
---

## Summary

Each notable event of a turn adds a report for the player it concerns. A
player can have at most 32 reports from one turn; any later report that turn
is dropped.

## When it runs

During `resolution`, at the point where another rule detects one of the
events listed under Parameters. All 12 places that record a report are inside
the whole-turn resolver [FND-EVENT-004].

## Parameters

- `player`: the player slot that receives the report, or -1.
- `report_type`: the kind of report, from the list below.
- `arg1`, `arg2`, `arg3`: the report's arguments, of type `INT16`, as the
  list gives them for each type.

| Value | Recorded when | Recipients | `arg1`, `arg2`, `arg3` | Recorded through |
|---|---|---|---|---|
| 0 | Never recorded; the compositor's empty case | None | None | None |
| 1 | A Crackdown is created in a sector | Each player with a gang in the sector when resolution began | sector, 0, 0 | RULE-EVENT-004 |
| 2 | A player takes control of a sector | The new owner | sector, previous owner (-1 for none), 0 | RULE-EVENT-012 |
| 3 | A player loses control of a sector | The previous owner | sector, new owner (0 after a third Crackdown), 0 | RULE-EVENT-013 |
| 4 | An Influence completes a site | The influencing player | sector, site slot, 0 | RULE-EVENT-006 |
| 5 | Research completes an item | The researching player | item, 0, 0 | RULE-EVENT-007 |
| 6 | Bribe, Equip or Hire fails for lack of cash | The player whose order failed | 1, sector, 0 for Bribe; 2, sector, gang definition for Equip; 4, gang definition, 0 for Hire | RULE-EVENT-008 (Bribe), RULE-EVENT-014 (Equip), RULE-EVENT-009 (Hire) |
| 7 | A Hire fails because the sector is full | The hiring player | sector, 0, 0 | RULE-EVENT-010 |
| 8 | A Hire fails because the player has the most gangs allowed | The hiring player | gang definition, 0, 0 | RULE-EVENT-011 |
| 9 | A player is eliminated | All six player slots | eliminated player, 0, 0 | RULE-EVENT-003 |

## Inputs

`last_turn_reports`, `last_turn_report_count`.

## Procedure

```text
if player == -1:
    return
let n = last_turn_report_count[player]
if n >= 32:
    return
let report = last_turn_reports[player * 32 + n]
report.occupied = 1
report.report_type = report_type
report.arg1 = arg1
report.arg2 = arg2
report.arg3 = arg3
last_turn_report_count[player] = n + 1
```

## Outputs

No return value. Fills the next free record of `player` (FMT-STATE-006) and
adds 1 to the player's count, or changes nothing when `player` is -1 or the
player already has 32 reports. The record's byte at offset 1 is not written.

## Edge cases

- The 33rd and later reports of a player in one resolution are dropped. No
  earlier report is moved or overwritten.
- A report addressed to -1, such as the loss of a sector that had no owner, is
  dropped.
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

None known.
