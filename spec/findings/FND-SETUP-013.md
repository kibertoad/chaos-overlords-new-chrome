---
id: FND-SETUP-013
title: The left half of the full local setup screen has fixed rectangles for ten scenario buttons, four time limits, four mentalities and four turn times, and the preference loader keeps a stale value for a missing registry entry
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00438DA5..0x00439562
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004384F4..0x00438B34
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00438B35..0x00438DA4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004384C0..0x004384F3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040EE8A..0x0040F63C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00468BBC..0x00468E11
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046439A..0x00464782
tool: Ghidra 12.1.3
environment: null
---

## Observation

Rectangles below are written `(x, y, width, height)` on the 640-by-460 drawing
surface. The executable builds them as top, left, bottom, right and tests them
with `PtInRect`, so the right and bottom edges are outside.

Opening. The setup initializer `0x004384C0` copies the preference byte
`0x00487858`, sign extended, into the scenario dword `0x004ABBE8`, sets the
dword `0x004A5EF8` to 52 (`0x004384D2`), draws the scenario description
(`0x00438B35`) and clears `0x00494830`. The full local setup `0x0040E0A0`
calls it after loading the background, then draws the selection lights
(`0x004384F4`) and copies the six player types, portraits and 12-byte names
from `0x00490680`, `0x00490678` and `0x00490630` into `0x004AB638`,
`0x004A5F00` and `0x004A2588`. A button press whose x is below 356
(`0x0040E32A`) goes to the left-panel handler `0x00438DA5`.

Left-panel handler. `0x00438DA5` copies the scenario, `0x004A5EF8`,
`0x00487850` and `0x00487854` into locals and tests, in this order:

| Control | Rectangle | Pressed image, source in the sheet loaded as resource `0x8C` | Value kept |
|---|---|---|---|
| Scenario button `k`, 0 to 9 | `(80 + 114 * (k % 2), 109 + 35 * (k / 2), 110, 32)` for `k` below 6, and `y = 110 + 35 * (k / 2)` from 6 | `(110 * (k % 2), 32 * (k / 2), 110, 32)` | scenario `k + 4` below 6, `k - 6` from 6 |
| Time limit 26, 52, 104, 208 | `(80, 285, 53, 23)`, `(137, 285, 53, 23)`, `(194, 285, 53, 23)`, `(251, 285, 53, 23)` | `(0, 256, 53, 24)`, `(53, 256, 53, 24)`, `(106, 256, 53, 24)`, `(159, 256, 53, 24)` | `0x004A5EF8` |
| Left column row `i`, 0 to 3 | `(80, 337 + 27 * i, 110, 24)` | `(0, 160 + 24 * i, 110, 24)` | `0x00487850 = i` |
| Right column row `i`, 0 to 3 | `(194, 337 + 27 * i, 110, 24)` | `(110, 160 + 24 * i, 110, 24)` | `0x00487854 = i` |

The four time-limit rectangles are tested only while the scenario global is
below 4 (`0x00438F27`). Otherwise a press in `(80, 284, 224, 24)` calls the
general sound helper `0x00464290` with 4 and selects nothing.

When a press lands in a control, the handler draws its pressed image, calls
`0x00464290` with 2, and polls input while the button is held, drawing the
pressed image when the pointer is inside the rectangle and the background when
it is outside. On release inside, it writes all five values at once: the
scenario to `0x004ABBE8` and, as a byte, to `0x00487858` (`0x00439532`), the
time limit to `0x004A5EF8`, and the two column rows to `0x00487850` and
`0x00487854`; then it redraws the description. It redraws the lights in
either case (`0x00439554`).

Lights. `0x004384F4` restores `(0, 0, 320, 460)` from the background and
draws an 8-by-16 light, source `(304, 138, 8, 16)` of the same sheet, at:
the scenario's light, x 182 for an even value and 296 for an odd one, y 109
for values 4 and 5, 144 for 6 and 7, 179 for 8 and 9, 215 for 0 and 1 and 250
for 2 and 3; when the scenario is below 4, the time limit's light at
`(125, 285)`, `(182, 285)`, `(239, 285)` or `(296, 285)` for 26, 52, 104 and
208; `(182, 337 + 27 * 0x00487850)`; and `(296, 337 + 27 * 0x00487854)`. A
scenario value outside 0 to 9 or a time limit outside the four leaves the
point uninitialised.

Description. `0x00438B35` fills `(84, 40, 216, 52)` with black, draws string
resource `scenario + 1` at `(84, 40)`, and string `scenario + 95` in up to
five lines at `(84, 52 + 8 * line)`, each line at most 36 characters and
broken at the last space before that limit.

Player strip and cards. The renderer `0x0040EE8A` draws each slot's 32-by-32
portrait for the top strip at `(360 + 36 * n, 38)`, source `(32 * portrait,
480, 32, 32)` of surface 6, where `portrait` is the byte `0x004A5F00 + n`. A
card is a 76-by-68 box composed off screen and copied to `(385 + 83 * (n %
2), 92 + 74 * (n / 2))`; a slot whose type is -1 or 1 gets the background
copied there instead. Inside the box: a 9-by-41 bar in the slot's colour at
`(0, 3)`, the face at `(12, 0)`, and the name, the Pascal string at
`0x004A2588 + 12 * n`, at `(45 - 3 * length, 61)`.

Portrait helpers. `0x00468D87` and `0x00468CFC` step the byte `0x004A5F00 +
slot` down and up by one, from 0 to 14 wrapping to 14 and from 14 wrapping to
0, and keep stepping while any of the six bytes, the slot's own included,
equals the candidate. `0x00468C0E` returns the lowest value from 0 to 14 that
no slot holds (it scans 14 down to 0 and keeps the last free one; its result
is undefined when none is free). `0x00468BBC` returns the lowest slot whose
type is -1, or -1.

Preference loader. `0x0046439A` opens `HKEY_LOCAL_MACHINE` key
`SOFTWARE\Stick Man Games\Chaos Overlords\1.0` for reading. When the key
opens, it reads thirteen values in a fixed order (`prefsDiff`, `prefsTimeLimit`
and `prefsObjective` are the ninth, tenth and eleventh) into one shared dword
local that starts at 0, and after each query stores that local into the
value's global, the low byte for these three, whatever the query returned. When the key does not
open, nothing is read and the globals keep their initialized values; the byte
`0x00487858` holds 0.

## Interpretation

The left column is the AI Mentality (`mentality`) and the right column the
turn time limit (`planning_limit_choice`). The top-left scenario button is
Kill 'Em All (4) and the bottom-right Dominance (3). A selection takes effect
only when the pressed button is released inside it, and each commit also
stores the scenario as the next session's preference.

A missing registry value does not leave its global alone: the global gets the
value of the nearest earlier value in the list that was found, or 0 if none
was. With no `prefsObjective` stored, `preferred_scenario` becomes whatever
`prefsTimeLimit` (or an earlier value) holds, truncated to a byte. A fresh
setup therefore starts on Greed only when the key is absent or no earlier
value is stored.

The portrait arrows skip the portraits other slots hold, wrap at both ends,
and include empty slots in the test; an empty slot's byte is 15 (FND-SETUP-002
reads 15 as the empty image) and is never a candidate.

## Alternatives

FND-SETUP-012 saw Kill 'Em All lit and found no `prefsObjective` value. The
registry of the maintainer's machine, read on 2026-09-25 through the 32-bit
view (`HKLM\SOFTWARE\WOW6432Node\Stick Man Games\Chaos Overlords\1.0`), holds
`prefsObjective` 4 and `prefsTimeLimit` 0; the capture agrees with a stored 4.
When that value was written, and whether the earlier search looked in the
32-bit view, is not recorded.

Surface 6 is taken to hold `DATA/PX16/PX00129` as SCR-SETUP-001 states; the
load of surface 6 is not followed here. Whether the text drawer's point is the
top-left of the text is not recorded.

## How to reproduce

In Ghidra, open `0x00438DA5`: the loop at `0x00438DD9` builds the ten scenario
rectangles, the four time-limit tests start at `0x00438F3E`, and the two
column loops at `0x004391DD` and `0x004392BC`; the commit is at `0x0043952A`
to `0x0043954A`. Open `0x004384F4` for the light positions, `0x00438B35` for
the description, `0x0040EE8A` for the strip and cards, and `0x0046439A` for the
registry reads. Read the key string at `0x00487914`.
