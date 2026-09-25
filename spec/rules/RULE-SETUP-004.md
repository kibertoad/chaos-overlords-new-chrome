---
id: RULE-SETUP-004
title: A new match draws every slot's reaction, sets the research, generates the city, the headquarters and the Right Hands, then applies the name modifiers
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-015, FND-RNG-005, FND-AI-006, FND-CITY-001, FND-CITY-002, FND-CITY-003, FND-SETUP-003, FND-SETUP-004, FND-SETUP-011, FND-RESEARCH-002]
conflicting: []
split_with: []
related: [RULE-RNG-002, RULE-CITY-001, RULE-CITY-002, RULE-CITY-003, RULE-CITY-004, RULE-SETUP-005, RULE-SETUP-006, RULE-SETUP-007, RULE-RESEARCH-002]
---

## Summary

Setting up a new match gives each player slot a reaction value used by the
computer players, sets every player's research, builds the city, places the
six headquarters and their Right Hands, and then, in a local game, applies
any name modifiers. Every random draw of a new
match after the portraits happens here, in a fixed order.

## When it runs

Once per new match, called by RULE-SETUP-001 between the two cash writes.

## Parameters

None.

## Inputs

`mentality`, `scenario`, `network_game`, `local_game`, `player_names`, and
the state of `rng` through `roll`.

## Procedure

```text
# Reactions, used by the computer players' attitudes. Every slot draws,
# human, computer or empty. Homicidal Maniac (mentality 3) makes no draw.
for player in 0..6:
    if mentality != 3:
        reaction[player] = roll(4) + 2
    else:
        reaction[player] = 0

# No draws
call RULE-RESEARCH-002()

call RULE-CITY-001()
call RULE-CITY-002()
let assigned = call RULE-CITY-003()
call RULE-CITY-004(assigned)

# The name modifiers, only in a local game (local_game is the joining side's
# flag, FND-NET-004). One scan sets all six flags of a player before the next
# player; none of the following draws from rng.
if network_game == 0 and local_game == 0:
    for player in 0..6:
        modifier_right_hands[player] = name_matches(player, modifier_name_right_hands)
        modifier_visibility[player] = name_matches(player, modifier_name_visibility)
        hire_force_modifier[player] = name_matches(player, modifier_name_hire_force)
        modifier_elite[player] = name_matches(player, modifier_name_elite)
        modifier_islands[player] = name_matches(player, modifier_name_islands)
        modifier_cash[player] = name_matches(player, modifier_name_cash)
    call RULE-SETUP-006()
    call RULE-SETUP-005()
```

## Outputs

No return value. Sets `reaction`, `research_remaining`, every sector's
Income, Tolerance, sites and headquarters owners, the Right Hands, and in a
local game the six modifier flags, any extra starting gangs and the permanent
Crackdowns of the island modifier. The draws are, in order: one `roll(4)` for each of
the six slots unless the Mentality is Homicidal Maniac, then those of RULE-CITY-001,
RULE-CITY-002 and RULE-CITY-003.

## Edge cases

At Homicidal Maniac the first draw of this rule is the first density centre
of RULE-CITY-001. The executable applies the extra gangs and the island
Crackdowns in one pass over the players, each player's gangs and then the
elite gangs and then the Crackdowns; the two rules write different records,
so calling them one after the other gives the same state. In a network game
the modifier flags keep whatever they held.

## What the sources say

SRC-MANUAL-GOG, page 14, says each player starts Eliminate with one gang, the
Right Hands, and says nothing of how the city is made.

## Differences between builds

None known.

## Open questions

- The initialization of the computer players' attitude matrix, which happens
  in the same loop as the reactions, is described with the
  computer players.
