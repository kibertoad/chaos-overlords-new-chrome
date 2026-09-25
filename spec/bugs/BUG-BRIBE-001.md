---
id: BUG-BRIBE-001
title: Bribe costs 3 instead of the manual's 5 and has no Tolerance cap
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unclear
player_reliance: unknown
evidence: [FND-BRIBE-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-BRIBE-001]
---

## Symptom

A Bribe takes 3 cash from the player, not the 5 the manual prints, and a
sector can be bribed to any Tolerance, well past the manual's maximum of 40.

## Trigger conditions

Any Bribe order carried out by a player with at least 3 cash.

## Mechanism

The Bribe case of the instant phase tests cash against 3, subtracts 3, and
adds 3 to the sector's Tolerance with no comparison against a maximum
(FND-BRIBE-001). The manual's rule charges 5 and stops the base Tolerance at
40 (SRC-MANUAL-GOG, page 50).

## Frequency

Every Bribe.

## Player reliance

Unknown. Repeated Bribes make a sector's Tolerance high enough to keep
Crackdowns away for many turns, which players may use.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether the price and the missing cap were a late design change the
  manual missed, or a mistake in the code, is not known. The Help file's
  description of Bribe has not been compared.
