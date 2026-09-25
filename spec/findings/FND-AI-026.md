---
id: FND-AI-026
title: Sector selector modes 7, 8 and 9 score owned sectors by site Support, Cash and Stealth
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408642
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
tool: Ghidra 12.1.3
environment: null
---

## Observation

Selectors `0x0C`, `0x0D` and `0x10` of `0x00402D70` return the Support, Cash
and Stealth of a sector's site in a given slot, read from the 62-byte site
definition records of `DATA/SITES`. Selector `0x1C` tests whether that site's
definition Resistance minus its influence progress is below 1 (the site is
finished).

In the shared sector selector `0x00408642`:

- mode 7 scores each sector the player owns with the sum of the positive
  Support of its unfinished sites, and scores it only when selector `0x6F` is
  0;
- mode 8 scores each owned sector with the sum of the positive Cash of its
  unfinished sites;
- mode 9 scores each owned sector with the sum of the positive Stealth of its
  finished sites.

Selector `0x6F` counts the player's gangs in the sector whose previous action
(planning record +5) is 9 (Influence); it scans all 81 planning records and
compares each gang's sector.

## Interpretation

Modes 7 and 8 send a gang to the nearest owned sector with the most Support or
Cash still to be gained by Influence, and mode 7 avoids sectors where another
gang is already influencing. Mode 9 looks for the nearest owned sector whose
finished sites give the most Stealth.

## Alternatives

Whether a sector with no qualifying site scores 0 and is then not a candidate
is read as yes, since the search stops at the first radius where any
candidate scores.

## How to reproduce

In `0x00402D70`, find the cases `0x0C`, `0x0D`, `0x10` and `0x1C`, which read
the site definition table with a 62-byte stride. In `0x00408642`, find the
mode 7, 8 and 9 cases of the scoring switch and their loops over three site
slots.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
