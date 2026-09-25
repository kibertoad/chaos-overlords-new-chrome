---
id: FND-COMLINK-004
title: Comlink View marks the shown message read and draws it from a self-contained 166-byte record
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045E04D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D61A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0049CA90..0x004A08D0
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The View helper `fn_0045E04D` receives the active player and the message
  count from `fn_0045D61A`.
- It first sets byte `+1` of the selected record, then scans the read bytes of
  all 16 records to update the global unread indicator.
- It copies the whole record from
  `0x0049CA90 + player * 0xA60 + cursor * 0xA6` before formatting it. The copy
  shows this layout:

| Offset | Size | Use |
|---|---|---|
| `+0` | 1 | Occupied flag. The unread scan in `fn_0045D61A` tests it before byte `+1`, and the recorder appends and compacts whole records |
| `+1` | 1 | Read flag. View sets it, and the 16 records are scanned for one where it is clear |
| `+2` | 2 | Zero-based turn of the message. View shows the year `2050 + turn / 52` and the week `turn % 52 + 1` |
| `+4` | 1 | Sender player slot. View indexes the 12-byte player-name table and the portrait and colour tables with it |
| `+5` | 160 | Message text. View copies four consecutive 40-byte rows and draws each one |
| `+165` | 1 | Copied with the record; View does not read it |

- The page header shows the one-based number of the selected record and the
  count. View draws the sender's name of up to 10 characters and the sender's
  32-by-32 portrait and colour, then the four rows. Record turn 0 is shown as
  the date `2050.01`.

## Interpretation

A record is a complete snapshot of what View shows. The part View reads holds
no recipient list, time of day or network message ID. The only state View
changes is the read flag of the message it shows.

## Alternatives

- The finding calls the sender's picture a 32-by-32 portrait; whether View
  draws it at that size or scales it is not recorded.

## How to reproduce

From `fn_0045D61A`, follow the call to `fn_0045E04D`. Read the store to byte
`+1`, the 16-step scan, the `0xA6`-byte copy, the four `0x28`-byte row copies,
and the date arithmetic with 52 and 2050.
