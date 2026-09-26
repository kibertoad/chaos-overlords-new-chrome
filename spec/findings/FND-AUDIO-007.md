---
id: FND-AUDIO-007
title: CD music opens a shareable cdaudio device in TMSF format, a timer poll restarts a stopped program, and the MCI notification changes nothing
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458290..0x004584B9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458ACC..0x00458F94
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004642BD..0x00464399
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004652A0..0x004653AD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462579..0x004637B7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045C67D..0x0045C695
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00461008..0x00461016
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046224A..0x00462269
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048778C..0x00487798
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487838..0x0048783C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487878..0x0048787B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487890
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494BF0..0x00494C17
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00497FE8..0x00497FFF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004980A0..0x004980BB
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. MCI command and flag values are
named after the Windows headers.

- Auxiliary devices. The sound setup `fn_00458290` (FND-AUDIO-006) asks
  `auxGetDevCapsA` for each device below `auxGetNumDevs`. A device whose
  `wTechnology` is 1 (`AUXCAPS_CDAUDIO`) has its index stored at `0x00497FFC`,
  and the byte at `0x00487794` is set when its `dwSupport` has bit 0
  (`AUXCAPS_VOLUME`). A device whose `wTechnology` is `0x20` has its index
  stored at `0x00494C14`, and `0x00487798` is set under the same test. No
  `AUXCAPS_` constant has the value `0x20`, and nothing reads `0x00487798`.
  Both indexes are 0 when no device matches.
- Volume. `fn_00458E68(v)` and `fn_00458B05(v)` shift the 16-bit argument left
  by 8, keep 16 bits, and pass `sx(w) << 16 + sx(w)` to `auxSetVolume`, for the
  CD device and the `0x20` device respectively. For a value of `0x8000` or more
  the sign extension borrows one from the high word, so the right channel is one
  below the left: level 6 gives 38400 and 38399, level 10 gives 64000 and 63999.
  `fn_00458E2F` and `fn_00458ACC` return the high byte of the left channel of
  `auxGetVolume` for the two devices.
- Open. `fn_00458EA6` closes the device first when the open byte at
  `0x0048778C` is set. It sends `MCI_OPEN` (`0x803`) with the flags `0x3102`
  (`MCI_OPEN_TYPE | MCI_OPEN_TYPE_ID | MCI_OPEN_SHAREABLE | MCI_WAIT`) and the
  device type `0x204` (`MCI_DEVTYPE_CD_AUDIO`) through the block at `0x00497FE8`.
  On success it sets the open byte, copies the device ID from `0x00497FEC` to
  `0x00494C10`, and sends `MCI_SET` (`0x80D`) with `MCI_SET_TIME_FORMAT |
  MCI_WAIT` (`0x402`) and the format 10 (`MCI_FORMAT_TMSF`) through the block at
  `0x004980A0`. A failed open leaves the device ID 0.
- Status. `fn_00458CEF` sends `MCI_STATUS` (`0x814`) with `MCI_STATUS_ITEM |
  MCI_WAIT` for item 4 (`MCI_STATUS_MODE`) through the block at `0x00494C00`,
  and returns 1 only when the call succeeds and the mode is `0x20E`
  (`MCI_MODE_PLAY`).
- Play. `fn_00458B43(first, last)` sends `MCI_STATUS` for item 1
  (`MCI_STATUS_LENGTH`) of track `last` with `MCI_TRACK`, ignoring the result,
  and then `MCI_PLAY` (`0x806`) with `MCI_NOTIFY | MCI_FROM | MCI_TO` (`0xD`)
  through the block at `0x004980B0`. The callback is the main window handle at
  `0x00493664`, the start is track `first` at 0:00:00, and the end is track
  `last` at the minutes, seconds and frames of the length just read.
- Stop and pause. `fn_00458CA0` sends `MCI_STOP` (`0x808`) and `fn_00458C10`
  sends `MCI_PAUSE` (`0x809`), each with `MCI_WAIT` and each only when the status
  is playing.
- Resume. `fn_00458C5F` sends `MCI_PLAY` with `MCI_NOTIFY` alone through the
  same block, with no start or end position, whatever the status.
- Fade. `fn_00458D54`, the only work of `fn_00464385`, calls `fn_00458CA0`
  when the status is not playing or the CD device has no volume support.
  Otherwise it reads the CD volume `v`, lowers it 32 times by `v / 32` (the
  divide truncates), waits in a `timeGetTime` loop until `17 * step`
  milliseconds after the start and handles one window message per step through
  `fn_0045C2CD`, then sets the volume to 0, stops the device and sets the volume
  back to `v`.
- Close. `fn_00458F3E` stops the device and sends `MCI_CLOSE` (`0x804`) when
  the open byte is set, clearing it on success.
- Selector. `fn_004642BD(mode)` compares the mode with `0x00487878`, which is
  -1 in the image. When they differ it fades the music out and, unless the mode
  is -1, stores it. Then, when the music-enabled byte at `0x00487838` (1 in the
  image) is set, it sets the pointer to the hourglass, plays tracks 2 to 2, 9
  to 9 or 3 to 8 for the stored mode 0, 1 or 2, and sets the arrow again. A
  stored mode outside 0 to 2 plays nothing.
- Levels. `fn_004652A0` sets `0x0048783C` from the effects level, passes
  `effects_level * 25` to `fn_00458B05` even at level 0, and checks item
  `effects_level + 1` of menu 7. It then handles music: at level 0 it clears
  `0x00487838` and fades the music out; otherwise it sets `0x00487838` and passes
  `music_level * 25` to `fn_00458E68`. It does not start a program. Last it
  checks item `music_level + 1` of menu 6. Its callers are the title
  initialization at `0x00461088`, the Music and Sound Effects menu commands at
  `0x0046266B` and `0x00462683`, which store the item number minus 1 as the
  level first, and the window activation at `0x0046291F`.
- Pump. In the event pump `fn_00462579`, the deactivation case calls
  `fn_00458C10` when music is enabled and sets the inactive byte at
  `0x00487890`. The activation case, when music is enabled, calls
  `fn_004652A0` and then `fn_00458C5F`, and clears `0x00487890`. On each pump
  call that finds timer slot 0 set (FND-UI-023), at `0x00462A9F`, it calls the
  selector with the current mode when music is enabled, the status is not
  playing and the window is not inactive.
- Notification. The window procedure `fn_0045C33B` handles message `0x3B9`
  (`MM_MCINOTIFY`) at `0x0045C67D` by writing 0 to `0x00487790`, which nothing
  reads, and returns 0.
- Startup and exit. The title initialization saves the two auxiliary volumes
  through `fn_00458ACC` and `fn_00458E2F` at `0x00461008` and `0x00461011`. The
  exit path at `0x0046224A` calls the preferences writer, fades the music out
  and restores both saved volumes through `fn_00458B05` and `fn_00458E68`.
- The globals from `0x00494BF0` to `0x00494BFF` are not audio state: the
  Combat Results handler `fn_00451F80` writes `0x00494BF0` and `fn_00458155`
  and `fn_0046BA84` use the bytes from `0x00494BF4`.

## Interpretation

The game plays CD audio through the MCI `cdaudio` device in track, minute,
second and frame positions, and each program plays from the start of its first
track to the end of its last. It learns that a program has ended by asking
MCI about six times a second, not from the notification it requests: the
notification handler has no effect on the music. Any time the device is not
playing while music is enabled and the window is active, the current program
starts again from its first track, and the pointer shows the hourglass while
the play command is sent.

Changing a level from 0 to a nonzero value sets the music-enabled byte, and the
next presentation tick restarts the current program. Music stops with a fade
of about half a second when the CD device supports volume, and at once
otherwise. The music level sets the volume of the auxiliary CD device, the
effects level that of a device the setup looks for by a technology value no
Windows device reports, so on most machines the effects volume goes to device 0.
The game puts both volumes back as it found them when the player exits from the
title.

After the window becomes active again, the resume command plays from the
paused position with no end, which by the MCI definition runs to the end of the
disc, past the last track of the program.

Without a disc, or without a CD device, every command fails silently. With music
enabled the poll then sends the status and play commands on every presentation
tick, with the pointer switched to the hourglass and back each time.

## Alternatives

- How the GOG build's replacement `winmm.dll` answers the status, play and
  resume commands, and so whether the resume really runs past the program's
  last track there, has not been observed.
- Whether a `wTechnology` of `0x20` was meant for another constant cannot be
  told from the executable.

## How to reproduce

Follow the references to `mciSendCommandA` and the `aux*` imports in
`0x00458290..0x00458F94` and read the command, flag and item constants pushed
before each call. In `0x00462579`, find the call to `0x004328BE` with 0 before
the test of `0x00487838`, the call to `0x00458CEF` and the call to
`0x004642BD` at `0x00462AE8`. In `0x0045C33B`, find the compare with `0x3B9`.
