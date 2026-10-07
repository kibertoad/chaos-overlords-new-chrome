---
id: FND-UI-043
title: Local human planning completion clears the seat's waiting light
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046CF38..0x0046D00D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F465..0x0046F55C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F44D
tool: Python 3.14.7 with Capstone 5.0.7
environment: null
---

## Observation

The ascending slot loop in `fn_0046E766` selects each human slot (type 0),
stores its index in `0x004ABC84` at `0x0046F52F`, calls the planning visit
`fn_0046FD80` at `0x0046F552`, and then calls `fn_0046CF38` at
`0x0046F557`. Function extents are recorded in FND-EXE-004.

The sender tests the joining flag `0x00482178` and hosting flag
`0x00487B58`, identified by FND-NET-004. After its network branches,
`0x0046CFD4..0x0046D003` tests both flags again. When both are zero,
`0x0046CFF2` reads the active slot from `0x004ABC84`, `0x0046CFF7` writes
1 to its byte in `0x004ABC88`, and `0x0046CFFE` calls the light painter
`fn_0041B4EA`. FND-UI-017 records that this painter fills the light black
when this byte is nonzero, even for a human seat.

Before the next round's slot visits, the six-slot reset writes 0 to the
same bytes at `0x0046F44D`. The final visits described by FND-UI-039 occur
after resolution and do not go through that next-round reset.

## Interpretation

Local human seats wait with a lit light until their planning visit returns.
A completed local seat has a black light while later seats are planning,
and the lights stay black through resolution and the final city visits.
The reset restores waiting lights for the next round. This corrects the
network-only description of the writes in FND-UI-017; the painter itself
was described correctly. The sender's local presentation side effect is
separate from the network sends catalogued in FND-NET-004.

## Alternatives

These branches establish the local write and reset, without establishing
network delivery timing or every interruption of a planning visit.

## How to reproduce

Verify the BLD-GOG-EN-1.1 hash, disassemble the ranges above from their
function boundaries, and follow the zero-flag branch of the sender. Check
the planning call, sender call, next-round reset and final-visit ordering
in the outer match loop. No network connection or modified executable is
needed.
