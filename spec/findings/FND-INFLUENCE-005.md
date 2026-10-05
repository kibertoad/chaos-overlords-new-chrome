---
id: FND-INFLUENCE-005
title: The Influence panel selects pattern 147 before it draws the sites, so a completed site shows through the dense pattern
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F886..0x0043F8D8
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the Influence handler `fn_0043F692` (FND-INFLUENCE-002), just before the
loop over the three site slots, `0x0043F886` stores `0x4CC2` in
`[ebp-0x44]`, `0x0043F8A0` passes it as all three components to
`fn_00425E99`, and `0x0043F8D3` passes the result to `fn_00449B20`. No other
call of `fn_00449B20` lies between that point and the mode 0 copy of a
completed site at `0x0043FA6C`.

## Interpretation

`fn_00425E99` keeps the high byte, `0x4C` or 76, which `fn_00449B20` maps to
bitmap resource 147 (FND-GFX-006). The mode 0 copy of a completed site
therefore uses pattern 147, the densest of the three, which keeps more of the
black fill than of the site's picture. This answers the open question of
FND-INFLUENCE-002 on which pattern applies.

## Alternatives

None known.

## How to reproduce

In `0x0043F692`, find the store of `0x4CC2` at `0x0043F886`, the call of
`0x00425E99` with it three times and the call of `0x00449B20` at
`0x0043F8D3`, then follow the loop to the call of `0x00427864` with 5, 7 and
mode 0 at `0x0043FA6C`.
