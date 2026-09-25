---
id: FND-AI-018
title: The strategic refresh makes an AI hostile to a player whose sectors it out-fights by more than 75 percent
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040A1A7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042085D
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x0040A1A7` rebuilds a 24-byte record for each ordered pair of players (row
stride `0x90`, pair stride `0x18`, observer first). For the active observer
and each other player:

- offset +0 counts every sector the other player owns;
- offset +2 counts those sectors where the observer's gangs present have a
  strictly greater sum of effective Combat + Defense than the owner's gangs
  there that the observer can see. Selectors `0xB0` and `0xB1` list only
  defenders whose visibility byte for the observer is 1; selectors `0x5E` and
  `0x47` list the observer's own gangs in the sector.

When both counts are positive, the test at `0x0040A816` is the signed integer
comparison `(advantaged * 100) / owned > 75`, so exactly 75 percent fails. The
test is entered for another player whose controller type is 0 or 3 when the
Mentality (selector `0x36`) is 1 or more, and for any other controller type
when the Mentality is below 2. On success `0x0040A859` sets byte +20 of the
pair record, and `0x0040A86D` writes -10 to the attitude of the observer
toward the other player. The only other reader of byte +20, at `0x0042085D`,
uses the same observer-major indexing.

## Interpretation

Each turn a computer player becomes fully hostile to any opponent whose
territory it could mostly overpower with the gangs it already has there. At
Goon it does this only toward other computer players, at Criminal toward
everyone, and at Crime Lord and Homicidal Maniac only toward humans. The flag
at +20 lets family 2 take Control of such a player's sector (FND-AI-032).

## Alternatives

The owned count at +0 is a 16-bit or 8-bit field; its width is not recorded.
Where the pair records are stored (their base address) is not recorded.

## How to reproduce

In `0x0040A1A7`, find the calls to `0x00402D70` with selector `0x36` at
`0x0040A734` and `0x0040A7B8`, the multiply by 100 and divide before the
comparison with 75 at `0x0040A816`, and the two stores at `0x0040A859` and
`0x0040A86D`.
