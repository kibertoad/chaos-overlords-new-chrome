---
id: BUG-AI-002
title: The computer players' neighbourhood scans read one element past the last sector, and a failed placement anchor reads before the first
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-AI-010, FND-AI-021, FND-AI-040]
conflicting: []
split_with: []
related: [RULE-AI-005, RULE-AI-013]
---

## Symptom

Around the bottom-left corner of the map the computer players judge danger
and free land from values that do not belong to any sector. A computer player
whose placement anchor could not be replaced places new gangs in a sector
outside the map.

## Trigger conditions

A neighbourhood scan centred on sector 56 or 57 tests the linear index 64,
the cell below sector 56, which the column test does not reject. A computer
player that owns no sector with room, free neighbours or an eligible
alternative gets anchor -1.

## Mechanism

The scans test indexes from 0 to 64 inclusive (`c < 65`), while the sectors
run from 0 to 63. At index 64 the owner read lands on byte 0 of the first
combat record and the Crackdown read on byte 5 of the second combat record, and
the weight read gives the next player's sector 0, or 0 for player 5. When every
pass of `choose_anchor` fails, the stored anchor decodes to -1: the next
refresh reads the owner byte 36 bytes before the sector list, and a hire placed
there goes to sector -1.

## Frequency

Every planning pass that scans a neighbourhood touching index 64; the anchor
case only for a computer player with no usable owned sector.

## Player reliance

Unknown.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- What the game does with a hire placed in sector -1 is not recorded.
- What the byte before the sector list holds (`g_004A08C4` in RULE-AI-013) is
  not recorded.
