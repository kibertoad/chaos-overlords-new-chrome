---
id: FND-AI-060
title: The family-7 handler's Attack test reads the attitude toward the drawn gang's player
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00436C70..0x00436EC0
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 7's handler `0x00436C70` has no jump table. At weight 10 it stores the
drawn gang (from selector `0x29` or `0x91`) in a local, passes the drawn
ordinal to selector `0x2B`, and when that returns nonzero divides the stored
gang number by 81 (`0x00436D9A..0x00436DA3`) and compares the player's
attitude toward the quotient with 0 (`0x00436DAE`). A negative attitude
writes Attack; a failed test jumps to `0x00436EC0`, the start of the path
taken at other weights, which applies the selector `0x6C` equipment gate.

## Interpretation

The owner test reads the drawn gang's player. The gang that selector `0x2B`
compares with (the same ordinal in the full pool) plays no part in it.

## Alternatives

None known.

## How to reproduce

Open `0x00436C70` in the instruction view and follow the local written after
the selector `0x91` call at `0x00436D79` to the division at `0x00436DA3` and
the attitude compare at `0x00436DAE`.
