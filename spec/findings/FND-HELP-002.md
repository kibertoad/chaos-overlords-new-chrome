---
id: FND-HELP-002
title: The help text uses nine fonts and 93 internal hotspots, 67 jumps and 26 popups, all resolved through the context tree
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
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0x10..0xE42
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0x1271..0x14E2
tool: WinHelp container parser written from the public description of the format
environment: null
---

## Observation

- The internal file `|FONT` (`0xBE80..0xBF34`) holds 2 face names and 9 font
  descriptors of the old 11-byte form.
- The internal file `|TOPIC` (`0x1604..0xBE80`) is compressed, and its text
  uses the phrase table in `|PhrIndex` (`0x1271..0x14E2`) and `|PhrImage`
  (`0x10..0xE42`). Decompressed and read with its formatting commands
  (`LinkData1`) beside its phrase-expanded text (`LinkData2`), it yields 779
  runs of text with uniform formatting and 93 internal hotspots: 67 topic
  jumps and 26 popups.
- Every hotspot's argument is a context hash that occurs in the 80-entry
  context tree (FND-HELP-001).
- The text uses no display tables, embedded pictures, links to other files or
  macro hotspots.

## Interpretation

A link names a context hash, not a topic number. Since a context can point
into the middle of a topic, a link resolves to the last topic that starts at
or before the context's offset. Resolved that way, the 26 popups land on short
definition passages that the contents file does not list, and the 67 jumps
land on ordinary topics.

## Alternatives

None known.

## How to reproduce

Decompress `|TOPIC` with the WinHelp LZ77 scheme and expand phrases from
`|PhrIndex` and `|PhrImage`, then walk the paragraph records and count the
hotspot commands, separating jumps from popups by their command bytes.
