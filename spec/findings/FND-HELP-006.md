---
id: FND-HELP-006
title: The help text has 418 paragraph records and nine Times New Roman font descriptors in four sizes
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0xBE80..0xBF34
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0x1604..0xBE80
tool: WinHelp container parser written from the public description of the format, with the field order checked against Wine's winhlp32 parser and the Halibut WinHelp writer
environment: null
---

## Observation

- The nine font descriptors of `|FONT` (`0xBE80..0xBF34`) all name Times New
  Roman in the Roman family, at 16, 20, 24 or 28 half-points. Each carries its
  attributes and foreground and background colours.
- Decompressed, `|TOPIC` (`0x1604..0xBE80`) holds 418 paragraph display
  records. Their paragraph flags occur as follows: `0x0000` 36, `0x0002` 92,
  `0x0004` 156, `0x0006` 15, `0x0008` 1, `0x000A` 2, `0x0010` 4, `0x0014` 4,
  `0x0018` 13, `0x001A` 3, `0x001C` 9, `0x0030` 4, `0x0034` 4, `0x003E` 16 and
  `0x0806` 59.
- The records use space before, space after, line spacing, left and right
  indents and centred alignment. None uses tab stops, borders, right alignment
  or keep-together.

## Interpretation

The records give the authored geometry of every paragraph in source units. They
do not give the pixel positions the Windows help program would draw, which also
depend on its font rasterization.

## Alternatives

None known.

## How to reproduce

Decompress `|TOPIC` as FND-HELP-002 describes, walk its paragraph records and
count their flag words and the optional fields each flag enables; read the
descriptors of `|FONT` in the old 11-byte form.
