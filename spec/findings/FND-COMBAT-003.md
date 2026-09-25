---
id: FND-COMBAT-003
title: Damage Inflicted is credited the full computed damage of every opening attack, and never retaliation
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00473CCC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A5ED8..0x004A5EF0
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The player-indexed array of 32-bit values at `0x004A5ED8` has one write in
  the resolver, at `0x00473CCC`, inside the attack block of `0x00472775`.
- In that block the opening attack's damage is computed, added to the target's
  damage for the phase, and then added at `0x00473CCC` to the attacking
  player's element of the array. No comparison with the target's Force or with
  its remaining Force comes between.
- The retaliation calculation that follows makes no write to the array.

## Interpretation

The array is the Damage Inflicted statistic. Each opening attack credits the
whole damage it computed, including damage beyond the target's Force and
damage from several attacks in the same phase that together kill the target
many times over. Retaliation damage is never credited. Only the application of
damage to Force stops at zero.

## Alternatives

None known.

## How to reproduce

List the writes to `0x004A5ED8..0x004A5EF0`; the only one in the resolver is at
`0x00473CCC`. Read the data flow into it from the attack damage value.
