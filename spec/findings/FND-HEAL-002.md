---
id: FND-HEAL-002
title: The gang command handler greys Heal in both order menus when the gang is at Force 10
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00415085..0x00415599
tool: Ghidra 12.1.3
environment: null
---

## Observation

The gang command handler `fn_00414D8C` (FND-EXE-004) builds the two order
menus for the gang the human picked. It finds the gang record through the card
slot table at `0x004ABC68`, indexed by the handler's argument, and the
`active_player` at `0x004ABC84`, then reads the record's Force byte (offset
`0x03` of the 32-byte record, FMT-STATE-001).

- At `0x00415085` it compares that Force with 10. When they are equal, the call
  at `0x00415092` passes group 2 and item 3 to `fn_0042548A`, which greys an
  item (FND-UI-021). Item 3 of menu 2, the recurring-order menu, is Heal.
- At `0x00415587` it makes the same comparison. When they are equal, the call
  at `0x00415594` passes group 1 and item 7 to `fn_0042548A`. Item 7 of menu 1,
  the one-off order menu, is Heal.

Below Force 10 the handler leaves both items as the earlier enabling calls set
them.

## Interpretation

This places the greying of Heal that FND-UI-021 describes for the gang card's
menus. The group bar's Heal changes only gangs below Force 10 (FND-UI-021), so
a human cannot give a gang at Force 10 a Heal order, one-off or recurring, from
either. Together with the turn-start clear of a recurring Heal at Force 10
(FND-TURN-004) and the computer's Heal plans, which all require Force below 10
(RULE-AI-019 to RULE-AI-031), a gang reaches the instant phase at Force 10
with a Heal order only if its Force rose after the order was given.

## Alternatives

- The two comparisons test equality with 10. A Force above 10 would leave Heal
  enabled, but no finding records a Force above 10.

## How to reproduce

In `0x00414D8C`, find the two reads of the byte at `0x00498DAB` indexed by the
card slot at `0x004ABC68` and the player at `0x004ABC84`, each followed by a
comparison with `0xA`: the one at `0x00415085` guards a call to `0x0042548A`
with arguments 2 and 3 at `0x00415092`, the one at `0x00415587` a call with
arguments 1 and 7 at `0x00415594`.
