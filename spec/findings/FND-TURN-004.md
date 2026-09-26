---
id: FND-TURN-004
title: At turn start, four recurring actions are cleared when they can no longer apply, and every inactive gang's recurring action is cleared
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
tool: Ghidra 12.1.3
environment: null
---

## Observation

Before copying each gang's recurring action (record byte `+10`) into its active
action (`+7`), the outer turn function `fn_0046E766` scans all six players and,
for each, all 81 roster slots in ascending order. A switch on the recurring
action has cases for exactly four values, each of which clears the recurring
action when its test holds:

- Control (4): the acting player already owns the gang's sector, or that
  sector's Crackdown byte is positive;
- Heal (7): the gang's Force equals 10;
- Influence (9): the target site's progress has reached its definition's base
  Resistance, or the sector's owner is no longer the acting player;
- Research (11): the chosen item's remaining-research byte for the player is 0.

After the switch, the recurring action is cleared whenever the gang's sector
byte is 100, the inactive value. The recurring action and target that are left
are then copied into the active action and target.

There is no case for Chaos or Hide. Bribe and Snitch have none either.

This scan runs before the same function's update of Crackdown durations and
before its rebuild of the sector records from completed sites.

## Interpretation

Chaos and Hide repeat until the player replaces or cancels them or the gang
dies. A recurring Control order that fails during resolution because police
arrived is dropped at the next turn start, even when the same turn start's
Crackdown update ends the Crackdown. Recurring Influence is dropped when the
sector is lost, and does not resume if the sector is won back. Bribe and Snitch
would repeat forever if they were ever stored as recurring actions, but no
human command path stores them (FND-TURN-002).

## Alternatives

For an inactive gang, the Control and Influence tests read the record of
sector 100, past the end of the 64 sector records, before the inactive test
clears the action. The value read does not matter, since the action is cleared
either way.

How the Influence target selects the site and the Research target selects the
item has not been recorded. The Crackdown-duration update in `fn_0046E766`
mentioned here has not been reconciled with the decrement near the end of the
whole-turn resolver (FND-POLICE-001); they may be the same work seen from two
places, or two separate updates.

## How to reproduce

In `fn_0046E766`, find the nested loops over 6 players and 81 gang records
that end by copying record bytes `+10` and `+11` into `+7` and `+8`. The switch
on byte `+10` has cases 4, 7, 9 and 11; after it, the comparison of record byte
`+2` with 100 clears `+10`.
