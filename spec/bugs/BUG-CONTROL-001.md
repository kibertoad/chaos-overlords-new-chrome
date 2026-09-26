---
id: BUG-CONTROL-001
title: A player who gave no Control order can be given a contested sector whose Income plus Support is negative
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-CONTROL-001, FND-CONTROL-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-CONTROL-001]
---

## Symptom

A sector changes hands to a player who never tried to take it, instead of to
the player whose gangs were ordered to Control it, or instead of staying with
its owner.

## Trigger conditions

Some active gang was ordered to Control the sector, the sector has no police
(`crackdown_turns` is 0), and its Income plus the Support of its influenced
sites adds up to less than 0, which takes sites with negative Support. The
defenders do not enter the sum [FND-CONTROL-003].

## Mechanism

The Control pass compares all six player slots in every sector it settles, and
a player without a Control order there enters with a pooled strength of 0
(FND-CONTROL-001). The pass subtracts Income plus Support from every pool and
adds the owner's defense only to the owner's own pool (FND-CONTROL-003), so a
non-owner without an order has margin `-(income + support)`, which is positive
when that sum is negative. Every such player ties with the others at that
margin, and a real challenger whose margin is lower or equal no longer wins
alone. The winner is drawn from all the tied players and written as the new
owner, and the sector's influenced sites are reset. The manual describes the
comparison only among players who try to control the sector.

## Frequency

Only in a sector where someone gave a Control order and no Crackdown is in
force, since other sectors are not settled, and only when the sector's Income
plus Support is negative [FND-CONTROL-003].

## Player reliance

No strategy guide or player source that relies on it is known.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- How often the shipped site table allows a negative sum. With Incomes of 3 to
  7 it needs completed sites whose Support adds up to less than -3 in one
  sector, and a sector never holds the same site twice (FND-CITY-002).
- At a sum of exactly 0 the players without an order have margin 0 and join a
  zero-margin draw with the neutral entry (RULE-CONTROL-001); whether that
  counts as this bug or as the zero-margin rule is a matter of wording.
