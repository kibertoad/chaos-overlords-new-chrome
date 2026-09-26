---
id: RULE-TURN-001
title: A turn is turn start, planning by each active player in slot order, then resolution
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-005, FND-TURN-006, FND-TURN-004, FND-HIDE-001, FND-UPKEEP-001, FND-GANG-001, FND-HIRE-001, FND-DETECT-001, FND-EVENT-001, FND-RNG-004, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-TURN-002, RULE-TURN-004, RULE-TURN-005, RULE-UPKEEP-001, RULE-SITE-001, RULE-GANG-001, RULE-HIRE-002, RULE-HIRE-003, RULE-DETECT-001, RULE-EVENT-001, RULE-AUDIO-006, RULE-AUDIO-007, RULE-OBJECTIVE-001, RULE-SETUP-001, RULE-AI-001]
---

## Summary

From the second turn on, every turn starts by renewing recurring orders,
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

`turn_order`, `player_active`, `controller`, `elapsed_turns`.

## Procedure

```text
# a new match sets elapsed_turns to 0; a loaded one keeps the saved value
let first_pass = true
while true:
    # turn_start
    if not first_pass:
        call RULE-TURN-004()
        call RULE-UPKEEP-001()
    first_pass = false
    call RULE-SITE-001()
    call RULE-GANG-001()
    call RULE-DETECT-001()
    # the turn-start sound (RULE-AUDIO-006) plays at the end of turn_start

    # a human slot whose player_active is 0 is marked for one last visit
    let humans = 0
    for each player in turn_order:
        if controller[player] == 0:
            humans = humans + 1
            if not player_active[player]:
                controller[player] = -2
    if humans == 0:
        # a local match with no human left ends here
        return

    # planning_phase: six per-player presentation bytes are cleared first
    for each player in turn_order:
        if not player_active[player] and controller[player] != -2:
            continue
        if controller[player] == 1:
            call RULE-HIRE-002(player)
            call RULE-AI-001(player)
        if controller[player] == 0:
            # the human player gives orders: RULE-HIRE-002 runs on entry,
            # each gang order is RULE-TURN-005 and each hire or snub
            # RULE-HIRE-003 (the Comlink alert, RULE-AUDIO-007, comes at the
            # start)
            continue
        if controller[player] == -2:
            # the eliminated human is shown the result once
            controller[player] = -1
        # controller 3, a remote human, has no branch here

    call RULE-EVENT-001()
    call RULE-TURN-002()
    # RULE-TURN-002 ends with RULE-OBJECTIVE-001; when it ends the match, each
    # local human is shown the final state and the loop stops after the
    # increment below
    elapsed_turns = elapsed_turns + 1
```

## Outputs

No return value. Each turn runs the turn-start rules, the planning of each
active player in slot order, and RULE-TURN-002, and then adds 1 to
`elapsed_turns`. Before the planning loop the game clears six per-player bytes
used by the presentation. Only when the match has ended does a second loop
over the slots show each local human the final state (FND-TURN-006).

## Edge cases

The first pass of the loop skips the recurring cleanup and Upkeep: players
plan their first turn with the cash they were given at setup, and a gang pays
its first Upkeep at the start of the turn after it is hired (FND-TURN-005,
FND-UPKEEP-001). The first pass after loading a saved match skips both as
well, so that turn starts with the orders and cash the save holds
(FND-TURN-006).

Visibility is rebuilt once, before anyone plans. The offer refill runs at each
player's planning entry, after it.

`elapsed_turns` is 0 during the first turn and counts completed resolutions.

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

- What the eliminated human's last visit and the final-state loop show is not
  recorded (RULE-OBJECTIVE-001 and the screens).
- The order of the Upkeep payment against the recurring cleanup inside
  turn start rests on the order of the loops in the outer turn function; no
  run of the original has confirmed it.
