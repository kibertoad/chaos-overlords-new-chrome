---
id: FND-GIVE-003
title: The Give recipient list fills no background, and dims an ineligible card with black through bitmap 146 from the card's corner
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448027..0x00448717
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00445A4F..0x00447ADA
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00448027(player, tech_needed)` (FND-GIVE-002) pushes surface 7, builds a
colour from 48,000 in each component with `fn_00425E99` (`0x00448053`) and
passes it straight to `fn_00449B20` (`0x00448080`). The colour is used for
nothing else. The function then runs its loop over the five list entries.
Where a gang definition's `tech_level` is below `tech_needed` it builds black
from three zeros and calls `fn_004266A6` with fill 1 and shade 0 (`0x004486FC`)
over surface 7 `(208, 159 + 36 * n)-(305, 193 + 36 * n)`.

Neither `fn_00448027` nor the Give handler `fn_00445A4F` calls the rectangle
fill `fn_00426575`; its 90 call sites lie in other functions.

## Interpretation

FND-GIVE-002 read the 48,000 colour as a fill of the list area. It is the
pattern selector for the dimming: through FND-GFX-006, 48,000 becomes 187 and
selects bitmap 146, rows `0x88` and `0x22`. An ineligible card, 97 by 34 at
screen `(312, 139 + 36 * n)`, is covered with black on 48 of every 64 pixels,
starting at the card's top-left corner, whose own pixel keeps the card. The
list has no background colour of its own: where no card is drawn, the panel
image `PX05015` shows.

## Alternatives

- FND-GFX-006 notes that the result at 8-bit depth depends on the palette.

## How to reproduce

In `0x00448027`, find the three pushes of `48000` (`0xBB80`) before the call
to `0x00425E99`, the call to `0x00449B20` that takes its result, the absence
of any call to `0x00426575`, and the call to `0x004266A6` with 1 and 0 on the
card rectangle `(0x9F + 36 * n, 0xD0, 0xC1 + 36 * n, 0x131)` in
`(top,left,bottom,right)` order. List the callers of `0x00426575`.
