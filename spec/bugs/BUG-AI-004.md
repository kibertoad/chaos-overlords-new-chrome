---
id: BUG-AI-004
title: At Goon, family-1 computer gangs never commit crimes in sectors of player 0
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unclear
player_reliance: unknown
evidence: [FND-AI-020]
conflicting: []
split_with: []
related: [RULE-AI-020]
---

## Symptom

At Goon, a family-1 computer gang standing in a sector owned by the computer
player in slot 0 moves on instead of raising Chaos or snitching there, while it
does commit crimes in sectors of the computer players in slots 1 to 5.

## Trigger conditions

Mentality 0 (Goon), player slot 0 is a computer player, and a family-1 gang of
another computer player stands in one of its sectors after a Control, Equip or
Snitch.

## Mechanism

The crime gate for a sector owned by a computer player tests that the owner is
not the acting player and is greater than 0, which leaves out player 0 as well
as neutral sectors (-1).

## Frequency

Every such planning decision.

## Player reliance

Unknown.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether the test is meant to leave out neutral sectors only (`>= 0`) is not
  known; the intent is recorded as unclear.
