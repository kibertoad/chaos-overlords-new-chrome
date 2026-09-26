---
id: FND-TURN-005
title: Players plan one after another in ascending slot order, and all resolution follows the planning loop
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FD80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004726C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the outer turn function `fn_0046E766`, after the Upkeep work, the code
clears six per-player bytes used by the presentation and then runs a loop with
a counter from 0 to 5. For a slot whose controller value is 1 it calls the
computer planning function `fn_00458FA0`; for controller value 0 it calls the
human planning handler `fn_0046FD80`. A slot that is inactive or eliminated
enters neither. A second loop later in the function, which handles results and
the handoff between players, also counts slots from 0 to 5.

Only after the planning loop does `fn_0046E766` call, at `0x0046F706`, the
function `fn_004726C0`, whose call at `0x00472750` enters the whole-turn
resolver `fn_00472775`. The resolver's action passes and its hire pass each
scan the player slots again from 0 to 5.

The Upkeep scan of `fn_0046E766` runs at the top of every pass of its loop
except the first, before the planning loop. A successful hire in the resolver
copies a whole 32-byte gang record into the first free roster slot and writes
its sector byte (record offset `+2`) with the chosen destination instead of
100. In the next pass of the outer loop, the Upkeep scan counts that gang as
active and subtracts its definition's Upkeep.

## Interpretation

The mix of human and computer players does not change the order of a turn.
Players who are still in the match plan in slot order, eliminated slots are
skipped, and every order is carried out afterwards, in the resolver's own scans
by slot. A hired gang costs no Upkeep in the turn it is hired and pays its
first Upkeep at the start of the next turn. Because the original returns
control to the player only after that Upkeep scan, the hire price and the first
Upkeep appear to be taken at the same moment.

## Alternatives

How the loop tells an inactive slot from an active one (the player's active
byte, or a controller value) has not been recorded. What the loop does for
controller value 3, a human playing over the network, is not part of this
finding. The instruction addresses of the six-byte clear, the planning loop and
the second slot loop have not been recorded.

## How to reproduce

In `fn_0046E766`, find the loop from 0 to 5 that tests each slot's controller
value and calls `fn_00458FA0` or `fn_0046FD80`. After it, the call at
`0x0046F706` goes to `fn_004726C0`, and that function's call at `0x00472750`
goes to `fn_00472775`.
