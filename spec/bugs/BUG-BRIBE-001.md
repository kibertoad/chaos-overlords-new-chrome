---
id: BUG-BRIBE-001
title: Bribe costs 3 instead of the manual's 5
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unclear
player_reliance: unknown
evidence: [FND-BRIBE-001, FND-TOLERANCE-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-BRIBE-001, RULE-TOLERANCE-002]
---

## Symptom

A Bribe takes 3 cash from the player, not the 5 the manual prints.

## Trigger conditions

Any Bribe order carried out by a player with at least 3 cash.

## Mechanism

The Bribe case of the instant phase tests cash against 3, subtracts 3, and
adds 3 to the sector's base Tolerance (FND-BRIBE-001, FND-TOLERANCE-001). The
manual's rule charges 5 (SRC-MANUAL-GOG, page 50).

The manual's maximum base Tolerance of 40 does apply, through the clamp to
1..40 that runs over every sector after the instant phase (FND-TOLERANCE-001,
RULE-TOLERANCE-002); the Bribe case itself has no comparison with 40. An earlier version of this entry,
resting on FND-BRIBE-001 alone, said there was no cap.

## Frequency

Every Bribe.

## Player reliance

Unknown. A cheaper Bribe makes it easier to keep a sector's Tolerance high
enough to keep Crackdowns away, which players may use.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether the price was a late design change the manual missed, or a mistake
  in the code, is not known. The Help file's description of Bribe has not been
  compared.
