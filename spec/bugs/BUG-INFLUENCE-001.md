---
id: BUG-INFLUENCE-001
title: A band-0 Influence sets the site's progress to its dice pool plus its successes
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-INFLUENCE-004, EXP-TURN-023]
conflicting: []
split_with: []
related: [RULE-INFLUENCE-001, RULE-AI-018]
---

## Symptom

At Mentality Goon, a computer player's gang can complete a site in one turn
of Influence that should take several, and a site the player has already
advanced can fall back.

## Trigger conditions

A gang of a band-0 player, that is a computer player at Goon, influences a
site that is not complete.

## Mechanism

The resolver holds the site's progress in a local, and the band-0 branch
reuses that local for its reduced dice pool before the successes are added to
it (FND-INFLUENCE-004). The progress written back is the pool, less a fifth,
plus the successes, capped at the Resistance.

## Frequency

Every band-0 Influence. In EXP-TURN-023 player 2's gang rolled 8 dice for 4
successes on a site of Resistance 12 with no progress, and the progress became
12.

## Player reliance

Unknown. It makes Goon computer players complete sites faster, the opposite
of what band 0 does to their other rolls.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

None.
