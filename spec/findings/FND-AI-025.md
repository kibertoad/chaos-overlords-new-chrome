---
id: FND-AI-025
title: Sector selector mode 6 routes toward the scenario leader and hostile human land
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408642
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the shared sector selector `0x00408642`, mode 6 scores each sector in two
independent parts. First, when at least one human plays (selector `0x32` is
above 0), a sector owned by a player the active player views negatively gets
+2, but only when that owner is human. Second, it adds one leader point:

- with a unique leader (selector `0x2E`) other than the active player, only
  that leader's sectors get +1;
- with no unique leader, every sector owned by a player whose standing is 0
  gets +1;
- when the active player is the unique leader, every sector owned by another
  player that holds fewer than four of the active player's gangs gets +1.

These scores then go through the same nearest-square search, tie draw and
one-step routing as the other nonzero modes (FND-AI-005, FND-AI-040).

## Interpretation

Mode 6 sends a gang toward the player who is winning, or, when its own player
is winning, toward weakly held enemy land, with an extra pull toward hostile
human players.

## Alternatives

Whether "sector owned by another player" in the leader case excludes neutral
sectors is read as yes (owned by a player). The mode 6 block's instruction
range is not recorded.

## How to reproduce

In `0x00408642`, find the case for mode 6 in the scoring switch: it calls
`0x00402D70` with selectors `0x32` and `0x2E`, reads the attitude matrix at
`0x004AB590`, and compares a gang count with 4.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
