---
id: FND-RNG-005
title: From an accepted local Begin to the first city, the draws are portraits, reactions, city and headquarters, in that order
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040E0A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00468C8E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CCF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046DC10
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475FE1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476726
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004677F0
tool: Ghidra 12.1.3
environment: null
---

## Observation

The local setup handler `fn_0040E0A0` accepts Begin, scans player slots 0 to 5,
and calls `fn_00468C8E` for each empty slot only. `fn_00468C8E` asks the bounded
wrapper `fn_0045D227` for a value from 1 to 15, subtracts 1, and asks again
while any slot already has that portrait.

When `fn_0040E0A0` returns true, its only caller `fn_00460CCF` loads a status
string and changes interface state, then calls the outer match function
`fn_0046E766` with the fresh-game flag set. A call-graph search eight levels
deep finds no path to `fn_0045D227` from the helpers called in between, or from
the calls `fn_0046E766` makes before its fresh-game branch.

In the fresh-game initializer `fn_0046DC10`, the first call that reaches
`fn_0045D227` is at `0x0046DC83`: one request for 1 to 4 for each of the six
players, a reaction draw, skipped for all six when the AI Mentality is
Homicidal Maniac. `fn_0046DC10` then calls the city generator `fn_00475FE1`,
which draws the 40 X and Y pairs of the density centres and then the sector and
site proposals, and last the headquarters permutation `fn_00476726`. These are
the only paths from `fn_0046DC10` to `fn_0045D227` before it returns. The first
hire offers are filled later, in `fn_0046E766`.

`fn_00468C8E` has one other caller, the network setup function `fn_004677F0`,
which the local path does not pass through. Local setup starts player 0 at
portrait 0, and a human's portrait changes are made through the interface, not
by a draw.

## Interpretation

Given the generator's state when a local Begin is accepted, the draws before
the first turn come in this order: the portrait draws for the empty slots,
including every rejected attempt; six reaction draws, or none under Homicidal
Maniac; the city's density centres and site proposals, including rejected
proposals; and the headquarters permutation. Nothing reseeds the generator on
the way, so its state at Begin is whatever the seed and the startup draws
(FND-RNG-001) and any earlier match or other drawing in the same process
left.

## Alternatives

The search for paths to `fn_0045D227` stopped at eight levels of calls. A
deeper path through the functions between Begin and the fresh-game branch is
unlikely but has not been excluded.

## How to reproduce

Find `fn_0040E0A0` from the setup screen's Begin button handling, and the call
to `fn_00468C8E` inside its slot loop. From `fn_00460CCF`, follow the call to
`fn_0046E766` that passes the fresh-game flag, and in it the call to
`fn_0046DC10`. The call at `0x0046DC83` is the first in `fn_0046DC10` that
reaches `fn_0045D227`; the calls to `fn_00475FE1` and `fn_00476726` follow it.
