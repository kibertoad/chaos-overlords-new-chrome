---
id: RULE-HIDE-001
title: A gang hides while its action is Hide, and each Hide carried out is counted for its player
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-HIDE-001, FND-HIDE-002, FND-AWARDS-001, FND-TURN-001, FND-TURN-002, FND-TURN-004, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

A gang is hidden for exactly as long as its order is Hide. It hides from the
moment the order is given, keeps hiding through the rest of that turn, and
stops at the next turn start unless the order was recurring. Each time the
instant phase reaches a hiding gang, its player's count of Hides, used for the
Big Fat Chicken award, goes up by one.

## When it runs

`is_hidden` is read in four places, all in `resolution`: the attack's Hide
test, the retaliation, the police chance, and the defending strength in
`control_phase` (FND-HIDE-002). The procedure below it runs in `instant_phase`,
for each gang whose `action` is `ACTION_HIDE` (RULE-TURN-003).

## Parameters

- `player`: the gang's player slot.
- `gang: FMT-STATE-001`: the gang carrying out Hide.

## Inputs

`hide_count`.

## Procedure

```text
define is_hidden(g: FMT-STATE-001) -> INT32:
    return g.action == ACTION_HIDE

hide_count[player] = hide_count[player] + 1
```

## Outputs

`is_hidden` returns true while the gang's `action` is `ACTION_HIDE`. The
procedure adds 1 to `hide_count[player]`. Makes no draws.

## Edge cases

The gang record has no hidden flag. Giving the Hide order during planning sets
`action` at once (RULE-TURN-005), so the gang is hidden before resolution
starts; replacing or cancelling the order during planning ends the hiding at
once.

At the next turn start `action` is set from `repeat_action` (RULE-TURN-004). A
one-off Hide has `repeat_action` `ACTION_NONE`, so the gang stops hiding then.
A recurring Hide has no end test, so the gang hides every turn until the player
changes the order or the gang dies, and is counted again every turn.

The count is made without any other test, so every Hide carried out counts.
The instant phase does nothing else for a hiding gang (FND-HIDE-002). Hiding
does not change what other players see.

## What the sources say

SRC-MANUAL-GOG, numbered page 31 (Hide), says Hide gives a gang a chance to
elude an attack altogether, that a hiding gang does not count toward Control of
a sector or toward resisting an enemy's Control, that it gets no retaliatory
attack when it is hit, and that hiding has no effect on whether the gang is
detected. It refers to Hiding on page 47; that section is on numbered pages 51
and 52, and gives the chance of being hit while hidden as 30 percent when the
attacker's Detect equals the hider's Stealth, moving 5 percent per point of
difference. The chance the executable uses belongs to the attack and police
rules. Numbered page 45 places Hide in the Instant phase; in the executable
the gang already hides during planning.

## Differences between builds

None known.

## Open questions

- The rules that read `is_hidden` (attack, retaliation, police, Control) are
  written in their own areas. The manual's claim that a hiding gang does not
  count toward resisting an enemy's Control agrees with FND-HIDE-002; its claim
  that a hiding gang does not count toward its own Control is moot, since a
  gang whose action is Hide is not carrying out Control.
