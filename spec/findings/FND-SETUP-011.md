---
id: FND-SETUP-011
title: One exact player name gives that player a detection baseline that sees every opposing gang in every sector
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
    address: 0x004AB588
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FA11
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463B5B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464060
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the same name comparison as FND-SETUP-004, `0x0046DC10` sets the
per-player byte at `0x004AB588 + player` for a player whose name equals the
fixed string the glossary calls `modifier_name_visibility`.

The visibility rebuild at `0x0046FA11` starts each viewer's 64 per-sector
detection values at -32000, or at 1000 when the viewer's byte at `0x004AB588`
is set. It then adds in the Detect of the viewer's active gangs as described
in FND-DETECT-001, and marks an opposing active gang visible when its Stealth
is no greater than the value for its sector.

The six-byte array at `0x004AB588` is written by the save writer at
`0x00463B5B` and read back by the save reader at `0x00464060`.

## Interpretation

The modifier lets its player see every opposing gang anywhere in the city,
for the whole match, including after a save and load.

## Alternatives

None known.

## How to reproduce

Follow the references to `0x004AB588`: the store in `0x0046DC10` after a name
comparison, the test in `0x0046FA11` that chooses between -32000 and 1000,
and the two block transfers in the save writer and reader.
