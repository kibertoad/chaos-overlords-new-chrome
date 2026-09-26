---
id: FND-AI-022
title: The turn resolver decodes a gang's two target bytes differently for each action
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the whole-turn resolver `0x00472775`, the two target bytes after a gang's
action byte are read as follows:

| Action | Target byte 1 | Target byte 2 |
|---|---|---|
| Attack (1) | target player | target player's roster slot |
| Equip (5) | item number | not used |
| Research (11) | item number | not used |
| Influence (9) | site slot in the gang's sector | not used |
| Move (10) | destination sector | not used |
| Give (6) | equipment mask: 1 weapon, 2 armor, 4 miscellaneous | the friendly target's roster slot |
| Sell (12) | the same equipment mask | not used |

Actions without a target leave both bytes 0. The resolver groups action 10 by
destination sector and charges action 3 against the sector's income. The
family handlers' own writes agree for Move, Equip, Attack, Influence and
Research. The places in the resolver are known only as positions in a
decompiler listing, in the Instant, Control and hire blocks and the Move pass.

## Interpretation

The AI planning records and the gang records use one encoding for targets, the
one given above, so a handler can write a target that the resolver reads
directly.

## Alternatives

Unused target bytes are read as not used; whether the resolver ignores them or
they carry values is not recorded.

## How to reproduce

In `0x00472775`, follow the switch on the gang's action byte (gang record +7)
and the reads of +8 and +9 in each case.
