---
id: FND-SETUP-001
title: Starting cash is $500 in Armageddon and $20 otherwise, and one exact player name overrides it with $1,500 after setup
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046EC72
tool: Ghidra 12.1.3
environment: null
---

## Observation

In its fresh-game path the outer match function `0x0046E766` sets every
player's cash to 500 when the scenario global `0x004ABBE8` is 9 and to 20
otherwise. An adjacent scan compares each player's name, byte for byte and
with case significant, with one fixed upper-case string held in the
executable (the glossary term `modifier_name_cash`), and sets a per-player
byte for a player whose name equals it. After the calls that set up the city
and the players return, the code at `0x0046EC72` reads that byte and
overwrites the flagged player's cash with `0x5DC` (1,500). The flag byte has
no reference from the save writer or reader.

## Interpretation

The modifier replaces both the ordinary and the Armageddon starting cash.
A name that differs only in case does not match. Nothing of the modifier
survives the start of the match except the cash itself.

## Alternatives

The address of the fixed string and of the per-player flag byte are not
recorded. Whether the cash assignment comes before or after the call into the
fresh-game initializer `0x0046DC10` is not stated; only the override is said
to follow it.

## How to reproduce

In `0x0046E766`, find the fresh-game branch that compares `0x004ABBE8` with 9
and stores 500 or 20 into the cash array `0x004A25E8`, then the loop that
compares each player name with the string, and the later block at
`0x0046EC72` that stores `0x5DC` for flagged players.
