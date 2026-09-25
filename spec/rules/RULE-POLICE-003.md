---
id: RULE-POLICE-003
title: Police presence counts down by one at the end of every turn unless it is permanent
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-CHAOS-002, FND-EXE-004, FND-POLICE-001, FND-POLICE-004, FND-SETUP-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-002]
---

## Summary

At the end of each turn, every sector with police loses one turn of police
presence, after that turn's police attacks. A permanent Crackdown does not
count down.

## When it runs

In `turn_end`, near the end of the resolver `fn_00472775` (range in
FND-EXE-004), after `combat_phase` and before the elimination check
`fn_00476F3B`: the decrement is at `0x00475E74` and the call of the
elimination helper at `0x00475ECD` [FND-POLICE-001, FND-CHAOS-002].

## Parameters

None.

## Inputs

Each sector's `crackdown_turns`.

## Procedure

```text
for s in 0..64:
    let c = sectors[s].crackdown_turns
    if c > 0 and c < CRACKDOWN_PERMANENT:
        sectors[s].crackdown_turns = c - 1
```

## Outputs

No return value. Lowers `crackdown_turns` by one where it is between 1 and 99.
No draws.

## Edge cases

- Presence added this turn by a neutralizing Crackdown (3 to 5 turns,
  RULE-POLICE-002) attacks in this turn's police phase and leaves 2 to 4 more
  police phases after this countdown. A Crackdown that fills a free history
  slot adds no presence [FND-POLICE-004].
- A value of 100 never changes.

## What the sources say

SRC-MANUAL-GOG, numbered page 43, says the police stay 3 to 5 turns. Counting
the turn of the Crackdown, the executable gives 3 to 5 police phases in all.

## Differences between builds

None known.

## Open questions

None known.
