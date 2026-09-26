---
id: FND-AI-035
title: The family-7 handler researches items and influences Research sites
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00436C70
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 7's handler `0x00436C70` starts from the cached opponent weight of the
current sector. At weight 10 it makes one draw, from the visible gangs of
human players in a sector owned by a hostile player and from every visible
opponent otherwise; the comparison uses the same ordinal in the full list.
Attack is written only when the comparison succeeds and the drawn target's
owner is viewed negatively; the first auxiliary value then stores the current
sector. A failed comparison continues into the research sequence.

Without an Attack it applies the selector-`0x6C` gate and the family-1 weapon
then armor choice; each slot needs its cooldown at most 0 and an affordable
different item, and writes a cooldown of the cost times 3 and a focus of -1. A
prepared Equip, Move, Attack or Influence skips the rest. Otherwise, a previous
Equip, Move, Attack or Influence clears the first byte of its previous target,
and Force below 8 with effective Heal at least -3 writes Heal.

Selector `0x30` starts from the current sector and replaces it only with an
owned sector whose cached Research score is strictly greater. That score is
the signed sum of the Research modifiers of the sector's three site
definitions. Ties keep the earlier candidate, and the current sector need not
be owned. A changed best sector is passed plus `0x40` for a one-step Move and
clears the focus. In the selected (current) sector, the first site in slot
order with positive Research and positive remaining Resistance gets Influence.
With none, the focus becomes the current sector and item Research begins.

A pending previous Research repeats the same item. A finished previous ranged,
blade or armor item next asks for blade, armor, or the fixed miscellaneous
list `[44, 41, 42, 43, 46, 50, 49, 52]`; every other type next asks for
ranged. A type scan takes the first positive item number of that type whose
Tech is at most selector `0x62`'s cap and whose research value for the player
is still positive. A failed continuation tries ranged, blade, melee, armor and
then the fixed list. When every category is exhausted the family becomes 0,
the gang Moves through mode 5, and the focus is cleared. In scenario 0 the
last three turns replace any result with Terminate.

## Interpretation

Family 7 is the researcher: it sits where the sites add the most Research,
influences Research sites there, and researches weapons, armor and
miscellaneous items in a fixed cycle until nothing is left.

## Alternatives

The list `[44, 41, 42, 43, 46, 50, 49, 52]` is a list of item record numbers;
which items they are is content and is not described here.

## How to reproduce

Open `0x00436C70`; the one mode 5 call is listed in FND-AI-028; the fixed list
is a constant array read in the miscellaneous branch; selector `0x30` and
`0x62` are cases of `0x00402D70`.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
