---
id: FND-TURN-002
title: Only two command handlers write the recurring action, and each assignment replaces the whole previous one
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414D8C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041462F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498DA8..0x0049CA68
tool: Ghidra 12.1.3
environment: null
---

## Observation

The gang records occupy `0x00498DA8..0x0049CA68`, 486 records of 32 bytes. A
record's recurring action is its byte `+10`, so the recurring action of the
first record is at `0x00498DB2`. The full list of references to that byte finds
writes made for a human player's orders in two functions only: the
individual-gang command handler `fn_00414D8C` and the sector-wide command
handler `fn_0041462F`.

The individual handler's recurring submenu offers Chaos (3), Control (4), Heal
(7), Hide (8), Influence (9), Research (11) and None (0). The sector-wide
handler's recurring submenu offers the same set without Research. Neither
offers Bribe (2) or Snitch (13).

When a recurring choice is made, the individual handler writes the action and
its target to `+10` and `+11`, and then writes the same action to the active
action `+7`. Choosing None writes 0 to `+10`. Its path for a one-off choice
writes 0 to the recurring action and target, except in its separate shortcut
that gives a recurring Influence.

The sector-wide handler starts its recurring value at 0. For an ordinary
choice it overwrites the active action and target of each gang it applies to
and leaves the recurring action 0. For a recurring choice it writes the chosen
action to both the active and the recurring action of each gang it applies to.

## Interpretation

A new order replaces the whole previous order: it does not keep the old
recurring flag or target. Bribe and Snitch can only be given as one-off
actions. The turn-start cleanup (FND-TURN-004) has no case for recurring
values 2 or 13, so such values would survive it, but no human command path can
store them.

## Alternatives

Which of the sector's gangs the sector-wide handler applies an order to has not
been recorded, nor whether its recurring path writes the recurring target
`+11` or the individual handler's recurring path writes the active target
`+8`. What the individual handler's recurring Influence shortcut is, and how
the player reaches it, has not been recorded.

The inventory classified the writes made for human orders. Whether the
computer players' planning code writes recurring actions, and which values it
can store there, is not part of this finding.

## How to reproduce

List the references to `0x00498DB2` (the recurring action of the first gang
record) in Ghidra. The writes in `fn_00414D8C` and `fn_0041462F` are the
human assignment paths. In each, the menu entries map to the action values
above through the constants written to `+7` and `+10`.
