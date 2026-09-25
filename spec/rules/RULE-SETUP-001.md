---
id: RULE-SETUP-001
title: A new match gives every player $20, or $500 in Armageddon, and $1,500 to a player with the cash modifier name
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-015, FND-SETUP-001, FND-SETUP-004, FND-RNG-005, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-SETUP-004]
---

## Summary

Every player starts a new match with $20, or with $500 in Armageddon. A player
whose name is exactly the executable's cash modifier name starts with $1,500
instead, whatever the scenario, but only in a local game.

## When it runs

Once per new match, when the outer match loop starts a fresh game after the
local setup's Begin has completed the roster (RULE-SETUP-003). It is the start
of the match; RULE-TURN-001 follows it.

## Parameters

None.

## Inputs

`scenario`, `modifier_cash`, and what RULE-SETUP-004 reads.

## Procedure

```text
# Compares a player's name, kept as a length byte followed by its
# characters, with a length-prefixed string held in the executable, length
# byte first. Case is significant. The executable compares every byte
# without stopping at a difference; the result is the same.
define name_matches(player, modifier) -> INT32:
    let name = player_names[player]
    if name[0] != modifier[0]:
        return false
    for i in 1..(name[0] + 1):
        if name[i] != modifier[i]:
            return false
    return true

for player in 0..6:
    if scenario == 9:
        # Armageddon
        cash[player] = 500
    else:
        cash[player] = 20

# The city and the players are set up; in a local game this sets
# modifier_cash
call RULE-SETUP-004()

for player in 0..6:
    if modifier_cash[player]:
        cash[player] = 1500
```

## Outputs

No return value. Sets `cash` of every player, then everything RULE-SETUP-004
sets, then overwrites `cash` of each player whose `modifier_cash` is set. Makes no draws
of its own; the draws of RULE-SETUP-004 fall between the two cash writes.

## Edge cases

A name that differs from the modifier only in case does not match. The
override replaces the Armageddon $500 as well as the ordinary $20. The
scan that sets `modifier_cash` runs only in a local game; with either network
flag set it is skipped and the flags keep whatever they held (FND-SETUP-015).

## What the sources say

SRC-MANUAL-GOG, page 14 (Armageddon), says every Overlord starts that scenario
with $500 and every item researched, which agrees with the executable. The
manual gives no ordinary starting cash and does not mention the name
modifiers.

## Differences between builds

None known.

## Open questions

- Whether `modifier_cash` is saved with the match, and whether a network
  match reaches this rule with a network flag set, are not recorded.
