---
id: FND-AI-029
title: The family-6 handler hunts hostile human sectors and attacks there with up to six target draws
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00431C60
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 6's handler `0x00431C60` starts from the cached visible-opponent weight
of the gang's current sector (selector `0x90`, FND-AI-013).

With a weight below 1 it writes Move. Selector `0x60` lists the sectors of
weight 10 in ascending order and takes the first one not covered according to
selector `0x5F`; the handler passes it plus `0x40` to the sector selector, or
mode 2 when there is none. It clears the first auxiliary value and stores the
routed one-step destination in the second.

With a positive weight it makes one target draw: from the visible gangs of
human players when the sector's owner is viewed negatively and the weight is
10, and from all visible opponents otherwise. Selector `0x2B` applies the
quarter-strength comparison (FND-AI-033) to the gang with the same ordinal in
the list of all visible opponents. A passing draw writes Attack at once. After
a failure it tries a weapon and then armor, each needing a cooldown at most 0
and writing a cooldown of the item's cost times 3. Then come a Heal branch and
a local Control or Move branch, which cannot be reached while the cached
positive weight stays unchanged, and then up to five more target draws, ending
in an Attack on the last drawn target even when every comparison failed.
Attack stores the current sector in the first auxiliary value; Equip and Move
clear it. In scenario 0 with fewer than four turns remaining the handler
replaces the result with Terminate (14).

## Interpretation

Family 6 is the hunter: it walks to the first sector where a hostile human's
gang is visible and no other hunter is heading, and fights whatever it finds
there.

## Alternatives

The thresholds of the unreachable Heal and Control branches are not recorded.
Whether the five further draws follow the same pool choice as the first is
assumed.

## How to reproduce

Open `0x00431C60`; find the selector `0x90` (or its cache `0xAF`) read, the
selector `0x60` and `0x5F` calls, the wrapper calls in the draw loops, and the
final comparison of scenario with 0 and turns remaining with 4.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
