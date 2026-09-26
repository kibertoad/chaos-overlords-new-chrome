---
id: FND-UI-034
title: The pointer is a stock Windows cursor, the arrow or the hourglass, never an image from the game's files
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465620
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465BC8
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The window setup `fn_00465620` gets its class cursor through `LoadCursorA`.
  No path in the executable loads a cursor from a `PX` file.
- The cursor helper `fn_00465BC8` remembers the shape it last set and maps five
  selector values to stock cursors: 0 `IDC_ARROW` (`0x7F00`), 1 `IDC_IBEAM`
  (`0x7F01`), 2 `IDC_CROSS` (`0x7F03`), 3 `IDC_NO` (`0x7F88`) and 4 `IDC_WAIT`
  (`0x7F02`). It loads the system cursor and passes it straight to
  `SetCursor`.
- The helper has 35 direct callers, all with a literal selector: 20 select the
  arrow and 15 the hourglass. None selects the I-beam, the cross or the no
  sign. The calls around the setup, load and resolution work, which run without
  taking input, pass the helper's force flag so that its remembered shape does
  not skip the change.

## Interpretation

The player sees the Windows arrow, and the Windows hourglass while the game is
busy setting up a city, loading a game or resolving a turn.

## Alternatives

None known.

## How to reproduce

List the callers of `0x00465BC8` and the constant each pushes. The table of
cursor IDs is in its switch.
