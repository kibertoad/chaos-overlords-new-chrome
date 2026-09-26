---
id: FND-UI-002
title: The Gangs in Sector panel shows every active gang of a roster in the sector at once, one 32-pixel column each
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044E6ED
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Gangs in Sector handler `fn_0044E6ED`, which loads `PX05009`, scans all 81
roster records for active gangs in the selected sector. For each match it
advances a column index `n` and draws the gang's portrait into a 32-by-32 cell
with corners `(144 + 32*n, 158)-(176 + 32*n, 190)`. In the same column it
writes Tech Level, Upkeep and the fourteen statistics, each as a two-cell
number whose left edge is at panel-local x `154 + 32*n`, screen x `258 + 32*n`.
The labels baked into `PX05009` put the sixteen value rows on screen baselines
172, 181, 191, 200, 209, 218, 228, 237, 246, 255, 264, 274, 283, 292, 301 and
310. A sector holds at most six friendly gangs, so at most six columns are
drawn.

## Interpretation

`PX05009` is a roster of all the gangs in the sector side by side, not a
browser that shows one gang at a time. Columns follow roster slot order.

## Alternatives

- Whose 81 records the scan visits (the active player's, or the owner of the
  sector) has not been recorded. Only the active player's gangs are expected.
- What happens with more than six matching gangs, if that can arise, has not
  been read.

## How to reproduce

Find the load of resource 5009 through the image loader `0x00464108` inside
`0x0044E6ED`, then the loop over 81 records and the constants 144, 158 and 32
passed to the rectangle helper.
