---
id: FND-COMBAT-009
title: Detailed Combat draws two Force tracks per gang, the starting and the shown Force, and Combat Results frames the chosen opponent's portrait
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042F779..0x0042F98A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043066C..0x0043087D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00454251..0x004543ED
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges are in FND-EXE-004. Surface 6 holds `PX00129` (FND-UI-031). The combat
panels are composed in surface 7 at `(0,144)` and copied to the screen at
`(104,124)`, so surface 7 `(x, y)` is panel-local `(x, y - 144)` (FND-UI-001).

Detailed Combat. `fn_0042F779` is called by `fn_0042EE46` (`0x0042F76A`) and
by `fn_00430C23` (`0x00431A55`); `fn_0043066C` by `fn_0042F98B`
(`0x0043065D`) and by `fn_00430C23` (`0x00431A5A`). Both take no argument and
draw two tracks into surface 7. Each track is drawn in two copies from
surface 6: first the whole 60-by-3 red track `(354,3)`, then `6 * value`
pixels of the 3-row green strip `(354,0)` from the same left edge.

| Function | Track | Value read (signed byte) | Surface 7 corner | Panel-local corner |
|---|---|---|---|---|
| `fn_0042F779` | upper | `0x00494771` | `(152,260)` | `(152,116)` |
| `fn_0042F779` | lower | `0x00494773` | `(152,267)` | `(152,123)` |
| `fn_0043066C` | upper | `0x00494579` | `(225,260)` | `(225,116)` |
| `fn_0043066C` | lower | `0x0049457B` | `(225,267)` | `(225,123)` |

The Detailed Combat driver `fn_0042E040` writes 8 bytes at `0x00494770` and 8
bytes at `0x00494578` as two dwords each (`0x0042E53D`, `0x0042E542`,
`0x0042E5B1`, `0x0042E5B6`) and
lowers the bytes at `0x00494773` and `0x0049457B` during a clip
(`0x0042E90C`, `0x0042E922`, `0x0042ED3A`, `0x0042ED50` and `0x0042E8E1`,
`0x0042E8F7`, `0x0042ED65`, `0x0042ED7B`).

Combat Results. `fn_00454251(opponent)` has eight call sites, all in the
Combat Results handler `fn_00451F80`, each after the page renderer
`fn_00453087` (FND-COMBAT-007) or a restore of the panel from surface 7. It
does nothing for -1. For `n` from 0 to 4 it copies the 34-by-34 cell
`(120,171)` of surface 6 with the keyed mode 1 of `fn_00427864`
(FND-PLATFORM-008) to the screen at `(305, 139 + 36 * n)`, one pixel outside
the opponent portrait `(306, 140 + 36 * n, 32, 32)`.

## Interpretation

Each gang in Detailed Combat has two tracks, one above the other, under its
portrait: the left gang's at panel-local x 152 and the right gang's at x 225,
both 60 pixels wide. The 8-byte blocks at `0x00494770` and `0x00494578` have
the layout of the first eight bytes of a combat record (FMT-STATE-003), for
the left and the right gang, so the upper track shows `force_start` and the
lower track shows `force_shown`, the value the clips lower. The upper track
therefore stays at the Force the gang had before the combat phase while the
lower one falls.

The tracks are at panel-local y 116 and 123, two pixels lower than the y 114
and 121 measured from the capture in FND-UI-010.

The chosen opponent in Combat Results is marked by the same 34-by-34 frame
the Attack picker (FND-ATTACK-002) and the Equip and Research panels
(FND-EQUIP-009) use.

## Alternatives

- That the two 8-byte blocks are copies of combat records rests on the
  matching offsets and on what the driver does with bytes 1 and 3; the copy's
  source has not been traced here.
- The two-pixel difference from FND-UI-010 may come from the capture's
  scaling or from where the capture's origin was taken; the capture's
  settings were not recorded.

## How to reproduce

In `0x0042F779`, find the byte reads of `0x00494771` and `0x00494773`, the
multiply by 6, the destinations of top `0x104` and `0x10B` with left `0x98`,
and the sources `(3,0x162,6,0x19E)` and `(0,0x162,3,...)`; `0x0043066C` is the
same with `0x00494579`, `0x0049457B` and left `0xE1`. In `0x00454251`, find the
five destinations of left `0x131` and the source `(0xAB,0x78,0xCD,0x9A)`.
