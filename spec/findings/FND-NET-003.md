---
id: FND-NET-003
title: The network screens build their button rectangles as top, left, bottom, right, and their progress bars are one pixel per unit up to 100
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004677F0..0x004688C9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00468E12..0x0046913C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040B9C0..0x0040C4C4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00456F80..0x00457AF2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040D3C0..0x0040D72E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040D72F..0x0040D9E1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046D22F..0x0046D77A
tool: Ghidra 12.1.3
environment: null
---

## Observation

The rectangle builder `0x00425EDF` packs its four arguments as top, left,
bottom, right, and the hit test `0x00449B78` passes them to `PtInRect` in that
order. Converted to `(x, y, width, height)`:

Host lobby `0x004677F0`. The four held-button tests, in order, and what a
release inside does:

| Control | Rectangle | Pressed image, surface 7 | Effect |
|---|---|---|---|
| Add | `(371, 254, 92, 24)` | `(220, 0, 92, 24)` | With fewer than four local seats, takes the lowest free portrait (`0x00468C0E`) and the lowest empty slot (`0x00468BBC`) and makes it a local seat; otherwise sound slot 4 |
| Remove | `(468, 254, 92, 24)` | `(220, 24, 92, 24)` | With at least two local seats, empties the last one added; otherwise sound slot 4 |
| Begin | `(370, 375, 92, 45)` | `(220, 48, 92, 45)` | Tests the ready byte (`0x004A27C0`) of the remote seats (type 3); the local seats are marked ready only when the last remote seat in slot order is ready, otherwise sound slot 4 |
| Cancel | `(468, 375, 92, 45)` | `(220, 93, 92, 45)` | Closes all twelve connections and leaves |

The held-button helper `0x00468E12` draws the pressed image at the same
rectangle, except that Add's is drawn at x 370. Once every slot's ready byte
is set, the lobby sets `0x00487B58`, makes each slot still showing portrait 15
a computer player with a random portrait and default name, and starts.

Joining lobby `0x0040B9C0`: Add `(225, 284, 92, 24)` asks the host for a seat,
Remove `(322, 284, 92, 24)` gives the last one back (refused below two),
Ready `(224, 345, 92, 45)` sets `0x0048FB70` and sends the seat names, and
Cancel `(322, 345, 92, 45)` sends a leave message and closes connection 0.

Waiting screen `0x00456F80`: Continue `(224, 230, 92, 45)` and Cancel
`(322, 230, 92, 45)`, side by side; Cancel closes the session and all twelve
connections.

Progress. `0x0040D3C0` copies a strip `progress` pixels wide and 3 high from
`(354, 0)` of surface 6 to `(298, 105)`. Its caller `0x0040D72F` passes four
times the number of 25 blocks received so far and 100 at the end. The frame
`DATA/PX16/PX00139` is loaded into surface 7 at `(0, 0, 220, 72)` and copied to
`(210, 60, 220, 72)` (`0x0046D3A0`). `0x0046D22F` sets each remote seat's
progress to 0 and every other seat's to 100, and sets a seat to 100 when its
connection finishes.

Spinner. On each raised timer-0 flag (`0x004328BE`, 1000 / 6 = 166 ms per
FND-COMLINK-005) the frame counter advances by one from 0 to 14 and wraps to
0, and cell `(48 * frame, 72, 48, 48)` of surface 7, where `DATA/PX16/PX00138`
is loaded at y 72, is copied to `(224, 72, 48, 48)` (`0x0046D612`); the
transfer path `0x0040CED0` does the same.

## Interpretation

The numbers FND-SETUP-006 records for these rectangles are top, left, height,
width. On the host lobby the four controls sit where the local setup screen
has Add Player, Remove Player, Begin and Cancel. On the waiting screen the two
controls are side by side. A full progress bar is 100 pixels wide, the width
of the empty bar art, and the spinner turns about six frames a second.

## Alternatives

The host's Begin test reads only the last remote seat's ready byte; whether
that is intended is not settled. The status texts are string resources and are
not reproduced here.

## How to reproduce

In Ghidra, open `0x00425EDF` and `0x00449B78` for the argument order. Open
`0x004677F0` at `0x00467EF8`, `0x00467FDE`, `0x004680C5` and `0x004681E1`;
`0x0040B9C0` at `0x0040BFDB` to `0x0040C219`; `0x00456F80` at `0x004573B3` and
`0x00457427`. Open `0x0040D3C0`, `0x0040D72F` and `0x0046D22F` for the bars and
the spinner.
