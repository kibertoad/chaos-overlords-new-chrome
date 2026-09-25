---
id: FND-SETUP-003
title: One exact player name puts every neutral sector under a permanent Crackdown at the start of the match
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
    address: 0x004ABC10
tool: Ghidra 12.1.3
environment: null
---

## Observation

The same exact, case-sensitive scan of the players' names at the start of a
fresh game (FND-SETUP-001) sets a transient byte at `0x004ABC10` for a player
whose name equals a second fixed string in the executable (the glossary term
`modifier_name_islands`). After the city is generated, all six headquarters
have owners and all six Right Hands exist, `0x0046DC10` scans sectors 0 to 63
once for each flagged player. Every sector whose owner byte is -1 gets the
value 100 in the byte at offset `0x0F` of its sector record (`0x004A08F7` for
sector 0). Owned headquarters sectors are left alone.

The whole-turn resolver reads the same byte as the number of turns of police
presence and decrements it only when it is positive and below 100. The flag
is cleared when a fresh setup starts or a match is torn down, and has no
reference from the save writer or reader.

## Interpretation

The modifier starts every neutral sector under a Crackdown that never ends.
Since it runs after the headquarters are owned, the six starting sectors are
not affected. It does not set any Chaos value of the sector.

## Alternatives

Whether `0x004ABC10` is one byte per player or a single byte is not stated
exactly; the scan "once for each flagged player" implies one byte per player.
Where the scan that sets it lives (in `0x0046E766` with the cash modifier, or
in `0x0046DC10`) is not recorded.

## How to reproduce

In `0x0046DC10`, after the call to the headquarters routine `0x00476726` and
the Right Hands creation, find the loop that tests `0x004ABC10` for each
player and, over 64 sectors, stores 100 at `0x004A08F7 + sector * 0x24` when
the owner byte at `0x004A08E8 + sector * 0x24` is -1.
