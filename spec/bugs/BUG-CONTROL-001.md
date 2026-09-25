---
id: BUG-CONTROL-001
title: A player who gave no Control order can be given a sector whose defense is negative
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-CONTROL-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-CONTROL-001]
---

## Symptom

A sector changes hands to a player who never tried to take it, instead of to
the player whose gangs were ordered to Control it, or instead of staying with
its owner.

## Trigger conditions

A sector's Income plus the Force and Control of its defending gangs plus the
Support of its influenced sites adds up to less than 0, which takes sites with
negative Support. The Control pass then settles that sector.

## Mechanism

The Control pass compares all six player slots, and a player without a Control
order enters with a pooled strength of 0 (FND-CONTROL-001). Its margin is then
`-(income + defense + support)`, which is positive when that sum is negative.
Every such player ties with the others at that margin, and a real challenger
whose margin is lower or equal no longer wins alone. The winner is drawn from
all the tied players and written as the new owner, and the sector's influenced
sites are reset. The manual describes the comparison only among players who try
to control the sector.

## Frequency

Every time the Control pass settles a sector with a negative sum. Whether that
happens only when someone gave a Control order there is not known
(RULE-CONTROL-001, Open questions).

## Player reliance

No strategy guide or player source that relies on it is known.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether sectors with no Control order are settled at all; if they are, a
  negative-sum sector can change owner every turn with nobody trying.
- How often the shipped site table allows a negative sum: it needs sites whose
  Support is negative enough to outweigh the sector's Income and defenders.
