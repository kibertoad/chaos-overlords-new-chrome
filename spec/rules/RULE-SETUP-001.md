---
id: RULE-SETUP-001
title: A new match gives every player $20, or $500 in Armageddon, and $1,500 to a player with the cash modifier name
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-001, FND-SETUP-004, FND-RNG-005, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-SETUP-004]
---

## Summary

Every player starts a new match with $20, or with $500 in Armageddon. A player
whose name is exactly the executable's cash modifier name starts with $1,500
instead, whatever the scenario.

## When it runs

Once per new match, when the outer match loop starts a fresh game after the
local setup's Begin has completed the roster (RULE-SETUP-003). It is the start
of the match; RULE-TURN-001 follows it.

## Parameters

None.

## Inputs

`scenario`, `player_names`, `modifier_name_cash`.

## Procedure

```text
# Compares a player's name, kept as a length byte followed by its
# characters, with a length-prefixed string held in the executable.
# Case is significant.
define name_matches(player, modifier) -> INT32:
    let name = player_names[player]
    if name[0] != modifier[0]:
        return false
    for i in 1..(name[0] + 1):
        if name[i] != modifier[i]:
            return false
    return true

let cash_modifier: UINT8[6] = [0, 0, 0, 0, 0, 0]
for each player in turn_order:
    if scenario == 9:
        # Armageddon
        cash[player] = 500
    else:
        cash[player] = 20
    cash_modifier[player] = name_matches(player, modifier_name_cash)

# The city and the players are set up
call RULE-SETUP-004()

for each player in turn_order:
    if cash_modifier[player]:
        cash[player] = 1500
```

## Outputs

No return value. Sets `cash` of every player, then everything RULE-SETUP-004
sets, then overwrites `cash` of each player whose name matched. Makes no draws
of its own; the draws of RULE-SETUP-004 fall between the two cash writes.

## Edge cases

A name that differs from the modifier only in case does not match. The
override replaces the Armageddon $500 as well as the ordinary $20. The
modifier leaves no flag behind: after this rule nothing but the cash shows
that it applied, and a saved game keeps only the cash.

## What the sources say

SRC-MANUAL-GOG, page 14 (Armageddon), says every Overlord starts that scenario
with $500 and every item researched, which agrees with the executable. The
manual gives no ordinary starting cash and does not mention the name
modifiers.

## Differences between builds

None known.

## Open questions

- The address of the string `modifier_name_cash` is not recorded.
- Whether the player name comparison reads the name as a length byte and
  characters, as written here, or compares the characters up to a terminator,
  is not recorded at instruction level. Both give the same result for names
  the setup screen can produce.
- Whether the base cash assignment comes before the call into RULE-SETUP-004
  or after it is not recorded; no step of RULE-SETUP-004 reads `cash`, so the
  order does not change the result.
