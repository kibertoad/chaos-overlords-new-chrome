---
id: RULE-TURN-002
title: Resolution carries out the orders in a fixed order of steps, each visiting players and roster slots in ascending order
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-005, FND-TURN-001, FND-CHAOS-001, FND-COMBAT-001, FND-EQUIP-002, FND-EQUIP-006, FND-MOVE-001, FND-CONTROL-001, FND-POLICE-001, FND-POLICE-002, FND-TURN-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-POLICE-004, RULE-TURN-003, RULE-CHAOS-001, RULE-COMBAT-002, RULE-EQUIP-002, RULE-CHAOS-002, RULE-TERMINATE-001, RULE-MOVE-001, RULE-CONTROL-001, RULE-HIRE-001, RULE-TURN-006]
---

## Summary

Once everyone has given orders, the game carries them out in one pass: the
instant actions, then the Chaos rolls and any Crackdowns, then combat and the
police, then equipment trades, then the Chaos income, then Terminate, Move,
Control and hiring, and last the end of the turn. Within each step it takes the
players in seat order and each player's gangs in roster order, whatever order
the orders were given in.

## When it runs

In each turn, after `planning_phase` (RULE-TURN-001). It is `resolution`.

## Parameters

None.

## Inputs

None of its own; each step's rule lists what it reads.

## Procedure

```text
call RULE-POLICE-004()
call RULE-TURN-003()          # instant_phase
call RULE-CHAOS-001()         # chaos_phase
call RULE-COMBAT-002()        # combat_phase, with police_phase inside it
call RULE-EQUIP-002()         # transaction_phase
call RULE-CHAOS-002()         # chaos_payout_phase
call RULE-TERMINATE-001()     # terminate_phase
call RULE-MOVE-001()          # move_phase
call RULE-CONTROL-001()       # control_phase
call RULE-HIRE-001()          # hire_phase
call RULE-TURN-006()          # turn_end
```

## Outputs

No return value. Runs the steps of `resolution` in the order above, with every
draw each step's rule makes, in that order. RULE-POLICE-004 first notes which
players have a gang in each sector, for the Crackdown reports of this
resolution.

## Edge cases

Because the steps visit gangs by slot, the order in which players gave their
orders never changes the result or the order of draws.

A Crackdown created in `chaos_phase` is already in force when the police step
of the same turn's `combat_phase` looks for gangs in Crackdown sectors
(FND-POLICE-001).

Cash changes during resolution are applied at each gang's place in the scan:
an Equip is paid from the cash the player has at that moment, so a Sell earlier
in the same scan can pay for it and a later one cannot. Chaos income arrives
after all transactions and before hiring (FND-EQUIP-006).

A site completed in `instant_phase` gives nothing until the next turn start
(FND-TURN-001).

## What the sources say

SRC-MANUAL-GOG, numbered page 45 (Command Sequence), divides the carrying out
of orders into six phases, each executed for all players at once: Instant
(Bribe, Heal, Hide, Influence, Research, Snitch), Combat (Attack), Transaction
(Equip, Give, Sell), Chaos, Movement (Move, Terminate) and Control. Numbered
page 44 puts Hire after all of them. The executable differs in three ways:

- the Chaos rolls and the Crackdowns they cause come before Combat, and only
  the Chaos income waits until after the transactions;
- Terminate is carried out, for every gang, before any Move;
- nothing is simultaneous: each step visits the players in slot order and each
  player's gangs in roster order.

## Differences between builds

None known.

## Open questions

- That `hire_phase` comes after `control_phase` rests on the order of the two
  blocks in the resolver, not on a traced path.
- The police step's exact test for a gang it visits (an active gang in a
  sector whose `crackdown_turns` is positive) has not been recorded.
- RULE-POLICE-004 and the second loop of RULE-EVENT-001 both run at the start
  of `resolution`; their order against each other is not recorded. Neither
  draws, and they touch different state.
- Where RULE-TOLERANCE-001, the return of Tolerance toward normal, runs in the
  turn is not recorded.
- Whether the countdown of police presence in `turn_end` comes before or after
  the elimination check is not known (RULE-TURN-006).
