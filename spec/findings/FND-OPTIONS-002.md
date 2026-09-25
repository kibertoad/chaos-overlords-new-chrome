---
id: FND-OPTIONS-002
title: Done warns with the PX05020 panel when Warn if Idle Gangs is on and an active gang of the active player has no action
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FD80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448718
tool: Ghidra 12.1.3
environment: null
---

## Observation

- On Done, the city handler `fn_0046FD80` scans all 81 gang slots. An active
  slot whose `action` byte is 0 is idle. When the byte at `0x00487860`
  (`prefsFreeGang`) is nonzero and such a slot belongs to the active player, it
  calls the two-choice panel `fn_00448718`.
- `fn_00448718` loads resource 5020 (`PX05020`) and draws it at
  `(104,124,344,209)`. It tests the panel-local rectangle Cancel
  `(33,137)-(82,159)` before OK `(33,169)-(82,191)`, both with the right and
  bottom edges excluded. A key event confirms on virtual key `0x0D` (Enter) or
  `0x2B` (`VK_EXECUTE`) and cancels only on `0x1B` (Escape). There is no other
  key.
- The panel opens and closes through the slide helpers `fn_0041953E` and
  `fn_004196F5`, which play slots 0 and 1 when Slide Panels is on
  (FND-UI-011).
- `PX05020` carries its wording in the art: a system warning that at least one
  gang has nothing to do, asking whether to end the turn. Cancel is above OK.

## Interpretation

Warn if Idle Gangs asks for confirmation before a turn ends with a gang left
without orders. Confirming ends the turn as Done would have; cancelling returns
to planning. The warning changes no rule: idle gangs stay idle.

## Alternatives

- Whether the scan stops at the first idle gang or goes on is not recorded; it
  makes no visible difference.
- Which of the 81 slots are scanned (the active player's, or every player's
  slots with a test of the owner) is read as "the active player's".

## How to reproduce

In `0x0046FD80`, find the test of `0x00487860` next to a loop of 81 over the
gang records and the call to `0x00448718`. In `0x00448718`, find the load of
resource 5020, the rectangle constants 33, 137, 82, 159, 169 and 191, and the
compares with `0x0D`, `0x2B` and `0x1B`.
