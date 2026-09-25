---
id: FND-BRIBE-001
title: Bribe needs and costs 3 cash and adds 3 to the sector's Tolerance with no cap
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

Inside the instant-phase switch of the whole-turn resolver `0x00472775`, the
case for action 2 (Bribe) compares the player's cash with 3. When cash is
below 3, it calls the report helper with the insufficient-cash report and
changes nothing else. Otherwise it subtracts 3 from the player's cash, adds 3
to the Tolerance byte of the sector record, and adds 3 to the player's cash
spent. The case contains no comparison with 40 and no other clamp.

## Interpretation

Bribe succeeds whenever the player has at least 3 cash at the moment the gang
acts, and then raises the sector's Tolerance by exactly 3, whatever it was
before. The manual's price of 5 and its cap of 40 on the base Tolerance are
not in the executable.

## Alternatives

None known. The exact text of the failure report comes from the Last Turn
report table (FND-EVENT-001), not from this case.

## How to reproduce

In `0x00472775`, find the instant-phase switch on the gang's action byte; its
case 2 compares cash at `0x004A25E8` with the constant 3 and adds 3 to cash
spent at `0x0049CA78`. The instruction addresses of the case have not been
written down.
