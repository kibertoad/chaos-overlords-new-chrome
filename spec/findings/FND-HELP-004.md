---
id: FND-HELP-004
title: CHAOS.CNT is a 75-line text contents file with 14 headings and 59 topic entries
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: HELP/CHAOS.CNT
    offset: 0x00..0x70E
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

- The file is 1,806 bytes of ASCII with no byte above `0x7F` and no tab. It
  holds 75 lines, each ended by CR LF, including the last.
- The first line starts with `:Base` and names `Chaos.hlp` followed by
  `>main`. The second starts with `:Title`.
- The other 73 lines start with a level digit and a space: 14 lines at level
  `1`, none of which contains `=`, and 59 lines at level `2`, each of which
  holds a title, `=` and a context name. The longest line is 56 bytes.

## Interpretation

This is a WinHelp 4 contents file: 14 headings with 59 topics under them,
each topic reached through the context name after `=` (FND-HELP-001). The
base file and the window named `main` apply to every entry.

## Alternatives

None known.

## How to reproduce

Split `HELP/CHAOS.CNT` at CR LF and count the lines by their first two bytes.
