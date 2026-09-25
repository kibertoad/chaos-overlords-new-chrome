---
id: FND-AI-056
title: Mode 4 of the sector selector scores a sector 1 when selector 0x2D accepts the owner query's value for the planning player
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408C59..0x00408CAA
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the sector selector `0x00408642`, the mode 4 branch at `0x00408C59`
computes the candidate sector from the radius offsets, passes it to selector
`0x21` (`0x00408C71`), and passes the player argument of `0x00408642` and that
result to selector `0x2D` (`0x00408C80`). When the result is nonzero it adds 1
to the candidate's score in the table at `0x0048A150` (`0x00408C9F`) and sets
the found flag; it then jumps to the common block at `0x00409894`. It reads
nothing else.

## Interpretation

Selector `0x21` is the owner query (FND-AI-052) and selector `0x2D` the search
of the standings bytes of FND-STATE-004. Mode 4 marks a sector whose owner, as
the query reports it, passes that search for the planning player. For a
neutral sector (-1) the search returns 0; for a sector under police presence
(-2) no standings byte equals -2, so the result depends only on where the
planning player's slot number first appears among the standings bytes.

## Alternatives

None known.

## How to reproduce

In `0x00408642`, find the call to `0x00402D70` with selector `0x2D` at
`0x00408C80` and read the block from `0x00408C59` to the jump at
`0x00408CAA`.
