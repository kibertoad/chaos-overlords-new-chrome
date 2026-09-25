---
id: FND-AWARDS-002
title: Every Hide the resolver carries out adds one to the player's Hide count, hidden or not
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
    address: 0x00472D00
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A25D0
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Hide count array at `0x004A25D0` has one writer in the resolver: the
action dispatcher `0x00472775` reaches the case for action 8 (Hide) at
`0x00472D00`, which adds one to the current player's element without testing
whether the gang is already hidden.

## Interpretation

Every Hide that resolves counts toward the Big Fat Chicken award, including a
recurring Hide carried out turn after turn.

## Alternatives

Whether any other code (outside the resolver) writes the array, other than to
clear it for a new match, is not stated.

## How to reproduce

List the references to `0x004A25D0`. The resolver's only write is in the Hide
case of `0x00472775`, at `0x00472D00`.
