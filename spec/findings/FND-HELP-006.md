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
tool: WinHelp container parser written from the public description of the format, with the field order checked against Wine's winhlp32 parser (SRC-WINHLP32-WINE) and the Halibut WinHelp writer
environment: null
---

## Observation

- The nine font descriptors of `|FONT` (`0xBE80..0xBF34`) all name Times New
  Roman in the Roman family, at 16, 20, 24 or 28 half-points. Each carries its
  attributes and foreground and background colours, and each names a face
  inside the face table.
- Decompressed, `|TOPIC` (`0x1604..0xBE80`) holds 418 paragraph display
  records. Their paragraph flags occur as follows: `0x0000` 36, `0x0002` 92,
  `0x0004` 156, `0x0006` 15, `0x0008` 1, `0x000A` 2, `0x0010` 4, `0x0014` 4,
  `0x0018` 13, `0x001A` 3, `0x001C` 9, `0x0030` 4, `0x0034` 4, `0x003E` 16 and
  `0x0806` 59.
- The records use space before, space after, line spacing, left and right
  indents and centred alignment. None uses tab stops, borders, right alignment,
  keep-together or a first-line indent.
- The values the records give, with the number of records giving each:
  - space before: 6 in 8, 12 in 175, 24 in 4;
  - space after: 4 in 8, 6 in 59, 12 in 178, 24 in 18;
  - line spacing: 16 in 16, 20 in 28, each equal to the size in half-points
    of the text the record holds;
  - left indent: 29 in 8, 43 in 33, 58 in 16;
  - right indent: 29 in 8, 58 in 16.
- The 59 centred records hold text at 28 half-points only, with a space before
  of 12 and a space after of 6.
- 66 of the records hold text after an end-of-paragraph command (`0x82`) that
  is not their last command. Split at those commands, the 418 records hold 544
  paragraphs, none of them empty.

## Interpretation

The records give the authored geometry of every paragraph. With old 11-byte
font descriptors, Wine's viewer and helpdeco read each distance as half-points
and convert it to twips as the value times 10 less 5 (SRC-WINHLP32-WINE,
SRC-HELPDECO), so the common space of 12 is 115 twips, a little under 6
points. Command `0x82` ends a paragraph and the text after it starts another
with the same formatting (SRC-WINHLP32-WINE), so every paragraph of a record
carries the record's space before and after. In RTF terms (SRC-RTF-15) every
line spacing here is positive and smaller than the line height of the text it
sets, so it does not change the spacing.

The records do not give the pixel positions the Windows help program would
draw, which also depend on its font rasterization, nor whether it adds the
space after one paragraph to the space before the next.

## Alternatives

None known.

## How to reproduce

Decompress `|TOPIC` as FND-HELP-002 describes, walk its paragraph records and
count their flag words and the values of the optional fields each flag enables;
split each record's text at its `0x82` commands and count the pieces. Read the
descriptors of `|FONT` in the old 11-byte form.
