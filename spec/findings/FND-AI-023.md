---
id: FND-AI-023
title: A resolved hire takes the first inactive roster slot below 80
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
tool: Ghidra 12.1.3
environment: null
---

## Observation

The hire block of the resolver `0x00472775` scans the hiring player's gang
records upward from slot 0 while the record's sector byte is not 100, with a
strict bound of 80. At the first record with sector 100 it copies the complete
new 32-byte gang record into that slot. When the first 80 slots are all in use
it takes the failure path.

## Interpretation

Hired gangs reuse the lowest free roster slot rather than extending the roster.
The AI planner resets a reused slot's planning record (FND-AI-019), so a new
gang does not inherit the family or action history of the gang that held the
slot before.

## Alternatives

Whether the scan starts at slot 0 or at slot 1 (slot 0 holds the Right Hands)
is recorded as slot 0; a live Right Hands keeps slot 0 in use either way.

## How to reproduce

In `0x00472775`, find the hire block and its loop that compares each gang
record's sector byte (stride `0x20` from `0x00498DAA`) with 100 and the loop
counter with 80 (`0x50`), followed by an eight-word copy.
