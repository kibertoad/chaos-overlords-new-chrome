---
id: FMT-HELP-002
title: Help contents file HELP/CHAOS.CNT
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["HELP/CHAOS.CNT"]
byte_order: null
size: null
text: true
definition: null
evidence: [FND-HELP-004, FND-HELP-001]
conflicting: []
split_with: []
related: []
---

## Layout

The contents file is ASCII text of 75 lines, each ended by CR LF, the last
included. No byte is above `0x7F` and no line holds a tab (FND-HELP-004). The
Windows help viewer reads it when it opens `Chaos.hlp` (FMT-HELP-001); the
executable never opens it. Each line is one of the kinds below, told apart by
its start.

| Key | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|
| `:Base ` | text | `base` | First line. The rest of the line names the help file, `Chaos.hlp`, followed by `>` and the window name `main`, which every entry opens in. | supported | FND-HELP-004 |
| `:Title ` | text | `title` | Second line. The rest of the line is the title the viewer shows over the contents. | supported | FND-HELP-004 |
| `1 ` | text | `heading` | A level-1 line with no `=`: a heading whose text is the rest of the line. The file holds 14. | supported | FND-HELP-004 |
| `2 ` | text `=` context | `topic` | A level-2 line under the heading before it: the topic title, `=`, and the context name of the topic it opens. The file holds 59, and every context name is in the help file's context tree. | supported | FND-HELP-004, FND-HELP-001 |

## Enumerations and flags

None.

## Differences between builds

None known.

## Coverage

`HELP/CHAOS.CNT` of BLD-GOG-EN-1.1, 1,806 bytes, was split into lines with a
script (FND-HELP-004); every line is of one of the four kinds and in the
order the table gives. Its context names were checked against the help
file's context tree (FND-HELP-001).

## Open questions

- WinHelp 4 contents files allow more line kinds (`:Index`, `:Link`,
  `:Include`, deeper levels, `@` window and file suffixes). None occurs in this
  file, and how the viewer treats them is left to the public description of
  the format.
