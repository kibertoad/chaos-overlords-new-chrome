---
id: FND-PLATFORM-007
title: The palette loader fills entries 10 to 245 of a 256-entry palette from a CLT file
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004282AA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487514..0x00487533
tool: Ghidra 12.1.3
environment: null
---

## Observation

Palette loader `0x004282AA` builds a 256-entry logical palette. It reads the
selected file named from the template `data\CLT00000` (two copies of the
string, at `0x00487514` and `0x00487524`) into entries 10 through 245 and marks
those entries with explicit and no-collapse flags. Entries 0 to 9 and 246 to
255 keep the system's reserved colours. The loader selects and realizes the
palette in the surface's device context and, when DirectDraw palette objects
exist, copies it to the DirectDraw surface as well.

## Interpretation

At 8-bit depth the game shows its images through a palette of 236 colours of
its own between the 20 colours Windows reserves. The only CLT file in the
build is `DATA/CLT00002`, which holds exactly 236 four-byte entries
(FND-DATA-004).

## Alternatives

Which number replaces `00000` in the template, and so whether `DATA/CLT00002`
is the file read, has not been traced. The flag value the loader writes has not
been compared with the byte stored in each entry of the file.

## How to reproduce

Follow the references to the string `data\CLT00000` at `0x00487514` to
`0x004282AA`.
