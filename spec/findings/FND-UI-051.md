---
id: FND-UI-051
title: While a slid-in panel is open the pump leaves the selection frame as it was when the panel came in
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004196E4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00419A97
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004633D8
tool: Ghidra 12.1.3
environment: null
---

## Observation

Three instructions of the executable reference the byte `0x004854C8`:

- `0x004196E4`, in the slide-in `fn_0041953E` (FND-UI-023), stores 1 after the
  whole panel has been copied to the screen;
- `0x00419A97`, in the slide-out `fn_004196F5`, stores 0 after the area has
  been restored;
- `0x004633D8`, in the event pump `fn_00462579`, reads it before the
  selection-frame copies of FND-UI-048 and skips them while it is not 0.

The pump's counter `0x00487804` still advances on every timed pass
(`0x00463713`), whether the frame was drawn or not.

## Interpretation

A panel brought in by the slide-in stops the selected-sector outline: the
frame stays the one the pump drew before the panel came in, which is the
frame for the counter at that moment, `((n + 7) % 8) / 4` (FND-UI-048), while
the counter goes on. A capture taken with such a panel open shows the frame for
the counter read at `0x004196E4`, not for the counter at the capture.

## Alternatives

None known.

## How to reproduce

List the references to `0x004854C8`. Read the store at `0x004196E4` at the end
of `0x0041953E`, the store at `0x00419A97` at the end of `0x004196F5`, and the
test at `0x004633D8` that jumps past the frame copies of `0x00462579`.
