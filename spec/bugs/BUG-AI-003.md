---
id: BUG-AI-003
title: A computer gang's pre-attack strength test is made on the gang at the same position in a different list
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unclear
player_reliance: unknown
evidence: [FND-AI-033, FND-AI-032, FND-AI-029, FND-AI-038]
conflicting: []
split_with: []
related: [RULE-AI-004]
---

## Symptom

A computer gang sometimes attacks a gang it could not beat, and sometimes
passes over one it could, because the strength test looked at another gang.

## Trigger conditions

A computer gang draws its target from a narrower pool than every visible
opponent (the gangs of human players, of players it is hostile to, or of the
sector's owner) in a sector where other visible gangs come earlier in player
slot order.

## Mechanism

The handler draws an ordinal k from the narrow pool and attacks the k-th gang
of that pool, but selector `0x2B` looks the ordinal up in the list of every
visible opponent and tests the k-th gang of that list. The two lists agree only
when every visible opponent is in the narrow pool, or the pools share their
first k gangs.

## Frequency

Whenever the two lists differ at the drawn position; the families with
five-draw loops then attack the last target drawn whatever the tests gave.

## Player reliance

Unknown.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether the designers meant the full-list lookup is not known; the intent is
  recorded as unclear.
