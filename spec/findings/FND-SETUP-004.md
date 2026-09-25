---
id: FND-SETUP-004
title: Two exact player names give that player five extra Force-10 gangs in its headquarters
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046DC10
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBD8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2788
tool: Ghidra 12.1.3
environment: null
---

## Observation

The fresh-game initializer `0x0046DC10` compares every player's name, which it
keeps as a length-prefixed string, with further fixed upper-case strings held
in the executable, with case significant.

A name equal to the string the glossary calls `modifier_name_right_hands` sets
a byte at `0x004ABBD8`. After the normal Right Hands in roster slot 0 is
created, roster slots 1 to 5 of that player each receive a gang of definition
0 at Force 10 with no equipment, in the player's headquarters sector.

A name equal to the string the glossary calls `modifier_name_elite` sets a
byte at `0x004A2788`. That player's roster slots 1 to 5 instead each receive a
gang of definition 59 at Force 10, with weapon item 23, armor item 37 and
miscellaneous item 52, in the headquarters sector.

The flag bytes are transient. The gang records they produce are ordinary
records, saved and loaded like any other.

## Interpretation

These two modifiers give a player a six-gang opening. They make no random
draws. Only one of the two can apply to a player, since a name equals at most
one string.

## Alternatives

Whether the flag bytes are per player (six bytes each) is not stated. Whether
the extra gangs' other bytes (action, visibility) are written differently from
the Right Hands is not recorded. If both flags could be set, which block runs
last is not recorded, but a single name cannot set both.

## How to reproduce

In `0x0046DC10`, find the name comparison loop that stores into `0x004ABBD8`
and `0x004A2788`, then the blocks after the Right Hands creation that loop
over roster slots 1 to 5 and store definition 0 or 59, Force 10, and the item
numbers 23, 37 and 52.
