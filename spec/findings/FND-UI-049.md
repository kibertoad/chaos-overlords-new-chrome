---
id: FND-UI-049
title: Site Information keys a frame over the site portrait and, for a site with a special effect, writes string 29 plus the effect under the Cash row
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C5FE..0x0044C660
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C6A4..0x0044C70A
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the Site Information handler `fn_0044C476` (FND-UI-005), after the site's
120-by-64 picture is copied from surface 5 to backing `(372,159)-(492,223)`:

- `0x0044C5FE` to `0x0044C660` copy the rectangle `(242,299)-(362,363)` of
  surface 6, the interface sheet `PX00129`, over the same backing rectangle
  with `fn_00427864` and its keyed flag set, so the sheet's exact white leaves
  the picture showing.
- `0x0044C6A4` tests the 16-bit `special` field of the site's definition
  (FMT-DATA-001, offset `0x3C`), copied into the frame at `[ebp-0x114]`. When it
  is not 0, `0x0044C6B3` to `0x0044C6C9` copy string resource `0x1D + special`
  with an exact length of 20 through `0x00466673`, and `0x0044C6D1` to
  `0x0044C705` write it with `fn_00413FD5` from backing `(504,234)`, screen
  `(288,214)`.

## Interpretation

The portrait carries the same kind of keyed frame as the site portraits of the
detailed sector screen (FND-UI-018), from another cell of the sheet. A site
whose `special` is 1, 2 or 3 gets a fifth line in the Data box, string 30, 31
or 32, each of which holds its label and its amount separated by spaces.

## Alternatives

None known.

## How to reproduce

In `0x0044C476`, find the copy of resource 5002, then the call of `0x00425EDF`
with `0x12B`, `0xF2`, `0x16B` and `0x16A` and the call of `0x00427864` with 6
and 1 that follows it. Find the test of `[ebp-0x114]` at `0x0044C6A4`, the
addition of `0x1D`, the length `0x14` and the point `0x1F8`, `0xEA`.
