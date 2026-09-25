---
id: FND-OBJECTIVE-002
title: A local human who has been eliminated sees a private elimination card, in slot order, before the slot is retired
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
    address: 0x0042C3F5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004642BD
tool: Ghidra 12.1.3
environment: null
---

## Observation

The outer match loop `0x0046E766` walks slots 0 to 5. When it reaches a local
human slot that has been eliminated and not yet retired, it shows the handoff
card `DATA/PX16/PX00132` if more than one local human is playing, then calls
`0x0042C3F5`, marks the slot retired, and carries on with the later slots.
Right after the call it asks the music selector `0x004642BD` for mode 2, the
ordinary gameplay music.

`0x0042C3F5` keeps the city screen as it is, lays `DATA/PX16/PX00200` at
`(106,25)` and then `DATA/PX16/PX00203` at `(110,30)`, draws the eliminated
player's name centred at `(158,46)`, and draws the player's 64-by-64 Overlord
portrait at `(126,54)`. It blocks until the Done control, the rectangle from
`(428,377)` up to but not including `(528,425)`, completes; a press elsewhere
on the card does nothing.

With a single local human, the loop skips the handoff card but still reaches
this presenter when that human is eliminated.

## Interpretation

Elimination of a local human is shown privately at the point that player's
turn would have come, not as a popup during resolution, and not skipped.
The Done rectangle is shared with the endgame screen (FND-AWARDS-003).

## Alternatives

Where in the turn this walk runs (after resolution, before the next planning
scan) is not recorded. The source of the portrait (which sheet and cell) and
the font of the name are not recorded. What the single-human path does after
the card (return to the title, as the manual says) is not recorded here.

## How to reproduce

In `0x0046E766`, find the slot walk that tests for an eliminated, unretired
local slot and calls `0x0042C3F5`, followed by a call to `0x004642BD` with 2.
In `0x0042C3F5`, find the loads of resources 200 and 203 with destinations
`(106,25)` and `(110,30)`, the centred text at `(158,46)`, the portrait at
`(126,54)`, and the Done rectangle test.
