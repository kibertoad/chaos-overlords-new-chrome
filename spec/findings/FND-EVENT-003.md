---
id: FND-EVENT-003
title: Last Turn reports other than Influence and Research load the illustration numbered 6000 plus the report type
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044FD6C
tool: Ghidra 12.1.3
environment: null
---

## Observation

The compositor `fn_0044FD6C` keeps the report type in a stack local. Types 4
and 5 have branches of their own. Every other type takes a common branch that
calls the image loader with the resource number `type + 6000`. The three cash
failures, all type 6 (FND-EVENT-001), therefore load `PX06006`, a 242-by-158
image.

## Interpretation

The Bribe, Equip and Hire cash-failure reports all show `PX06006` in the
panel's illustration area. The area is never left empty for them.

## Alternatives

None known.

## How to reproduce

In `fn_0044FD6C`, follow the switch on the report type past the type-4 and
type-5 branches to the call of the image loader, and read the addition of 6000
to the type.
