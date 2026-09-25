---
id: RULE-TURN-001
title: A turn is turn start, planning by each active player in slot order, then resolution
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-005, FND-TURN-004, FND-HIDE-001, FND-UPKEEP-001, FND-GANG-001, FND-HIRE-001, FND-DETECT-001, FND-EVENT-001, FND-RNG-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-TURN-002, RULE-TURN-004, RULE-TURN-005, RULE-UPKEEP-001, RULE-SITE-001, RULE-GANG-001, RULE-HIRE-002, RULE-HIRE-003, RULE-DETECT-001, RULE-EVENT-001, RULE-AUDIO-006, RULE-AUDIO-007, RULE-OBJECTIVE-001, RULE-SETUP-001, RULE-AI-001]
---

## Summary

Every turn starts by renewing recurring orders and, from the second turn on,
paying Upkeep and collecting income. Then each player still in the game gives
orders, one after another in seat order, human or computer alike. Only when all
have finished are the orders carried out.

## When it runs

From the start of a match until it ends: after new-game setup (RULE-SETUP-001)
has built the city, or after a saved match is loaded. Each pass of the loop is
one turn.

## Parameters

None.

## Inputs

`turn_order`, `player_active`, `controller`.

## Procedure

```text
let first_pass = true
while true:
    # turn_start
    call RULE-TURN-004()
    if not first_pass:
        call RULE-UPKEEP-001()
    first_pass = false
    call RULE-SITE-001()
    call RULE-GANG-001()
    # the turn-start sound (RULE-AUDIO-006) plays at the end of turn_start

    # planning_phase: six per-player presentation bytes are cleared first
    for each player in turn_order:
        if not player_active[player]:
            continue
        call RULE-HIRE-002(player)
        call RULE-DETECT-001()
        if controller[player] == 1:
            call RULE-AI-001(player)
            continue
        if controller[player] == 0:
            # the human player gives orders: each gang order is RULE-TURN-005
            # and each hire or snub RULE-HIRE-003 (the Comlink alert,
            # RULE-AUDIO-007, comes at the start)
            continue

    call RULE-EVENT-001()
    call RULE-TURN-002()
    # RULE-TURN-002 ends with RULE-OBJECTIVE-001, which ends the match and this
    # loop when the objective is met
```

## Outputs

No return value. Each turn runs the turn-start rules, the planning of each
active player in slot order, and RULE-TURN-002. Before the planning loop the
game clears six per-player bytes used by the presentation. After resolution, a
second loop over the slots in the same order handles the results and the
handoff between players.

## Edge cases

The first pass of the loop skips Upkeep: players plan their first turn with the
cash they were given at setup, and a gang pays its first Upkeep at the start of
the turn after it is hired (FND-TURN-005, FND-UPKEEP-001).

An eliminated player's slot is skipped in planning. The order of planning does
not depend on which players are human.

The computer players draw from `rng` while they plan (FND-RNG-004), and so does
the offer refill of every planning entry, so the state resolution starts from
depends on every player's planning before it.

## What the sources say

SRC-MANUAL-GOG, numbered page 44 (The Structure of a Turn), lists five phases
in order: Upkeep, Command, Execution, Hire and Player Elimination. It agrees
with the executable on the order, but the executable carries out hires inside
its one resolution pass, after Control (RULE-TURN-002), and players in the
Command phase give orders one after another rather than together. The manual
does not say that the first turn has no Upkeep.

## Differences between builds

None known.

## Open questions

- Whether the first pass also skips Upkeep when the loop is entered from a
  loaded save, rather than from a new game, is not known.
- FND-TURN-004 places a Crackdown-duration update in the outer turn function,
  after the recurring cleanup; FND-POLICE-001 places the decrement near the end
  of resolution (RULE-POLICE-003). Whether these are one update or two is not
  known.
- How the planning loop tests that a slot is inactive (the `player_active`
  byte or the `controller` value), and what it does for `controller` value 3,
  a human playing over the network, is not known.
- The order of the Upkeep payment against the recurring cleanup inside
  turn start rests on the order of the loops in the outer turn function; no
  run of the original has confirmed it.
- The order of the offer refill and the visibility rebuild at a planning entry
  is not recorded.
- Where `elapsed_turns` is incremented in the loop is not known.
