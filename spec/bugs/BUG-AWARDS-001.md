---
id: BUG-AWARDS-001
title: The endgame screen shows at most three awards per player though a player can earn five
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: presentation
intent: unclear
player_reliance: unknown
evidence: [FND-AWARDS-001]
conflicting: []
split_with: []
related: [RULE-AWARDS-001, SCR-AWARDS-001]
---

## Symptom

A player who earns four or five awards sees only three of them on the
endgame screen. The missing ones are the last in the builder's order: Dollar
Sign and Safe.

## Trigger conditions

One player earns four or more awards. The easiest case is a match in which no
player spends any cash: every player then gets both Dollar Sign and Safe, so
a player who also leads Overthrows and damage has four.

## Mechanism

The award builder writes every award a player earns into a per-player table
that can hold five, but the endgame renderer reads only the first three
entries of each row.

## Frequency

Whenever a player earns more than three awards.

## Player reliance

None known.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether the three-entry limit was meant, since Dollar Sign and Safe can go
  to the same player only when every player spent the same amount, is not
  recorded.
