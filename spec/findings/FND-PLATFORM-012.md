---
id: FND-PLATFORM-012
title: The startup disc check looks for a fixed drive from the string ".\" and always passes, and the CD track search has no callers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046638E..0x0046658C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046658D..0x004665A6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046678D..0x004667DB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004667DC..0x0046690F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487B3C..0x00487B55
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498658..0x0049875B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498760..0x00498770
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046117A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470907
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004.

- The data at `0x00487B3C` holds three strings: `.\` padded to four bytes,
  `A:\` at `0x00487B40`, and ` A:\CHAOS\CDTrack` at `0x00487B44`, whose first
  byte is a space.
- The check `fn_0046638E` copies the four bytes at `0x00487B3C` to `0x00498760`
  and loops. Each pass calls `GetDriveTypeA` on the string at `0x00498760`. When
  the answer is 3 (`DRIVE_FIXED`) it calls `GetVolumeInformationA` on the same
  string, with a 260-byte volume-name buffer at `0x00498658`, and ends the loop.
  Any other answer adds 1 to the first character. When the first character
  reaches `Z` without a fixed answer, it writes 0 to the first character and
  ends the loop. The volume name, serial number, flags and file system name are
  not compared with anything, and nothing else reads `0x00498658`. The function
  returns 1 on every path; Ghidra drops one block after the loop as unreachable.
- The string the loop tests is two characters, a character and a backslash,
  with no colon: `.\`, then `/\`, `0\` and so on up to `Y\`.
- The two callers test the result. The title initialization at `0x0046117A`
  jumps to the shutdown path at `0x00462294` on 0. The planning function
  `fn_0046FD80` at `0x00470907`, after a human's planning ends, repeats the
  check on 0, shows a message through `fn_00465CEC` and can call the save
  function `fn_00463CC5`. Neither branch can
  be reached.
- The only other readers of `0x00498760` are in the intro `fn_004329C0`, which
  copies the movie names to `0x00498762`, two bytes after the start
  (FND-VIDEO-002).
- `fn_0046658D`, called on the shutdown path at `0x004622A8`, returns 0 and does
  nothing else.
- The CD search `fn_004667DC` has no callers and no data reference. It copies
  the string at `0x00487B44` to a local buffer, calls `GetLogicalDrives`, and
  for each drive from index 2 (`C:`) to 25 (`Z:`) whose bit is set asks
  `fn_0046678D`, which copies `A:\` from `0x00487B40`, adds the index to the
  letter and returns whether `GetDriveTypeA` answers 5 (`DRIVE_CDROM`). For a
  CD drive it puts the drive letter over the `A`, turns the leading space into
  the length byte through `fn_00449C41`, opens `X:\CHAOS\CDTrack` through the
  file helper, reads one byte and keeps that byte minus `'0'`. It goes on to
  the last drive and returns the value from the last CD drive that had the
  file, or 0.
- No string in the executable names a volume label.

## Interpretation

The startup check is the remains of a test for the game's disc. As built it
never looks at a CD drive and never fails, so the game starts and plays
without a disc in any drive. Its only lasting effect is the prefix it leaves at
`0x00498760` for the two movie paths: `.\` when Windows reports the current
directory's drive as fixed on the first pass, which makes the movies load from
`.\Data\`. Other answers change the first character, and a loop that runs out
leaves an empty prefix, so the movie names become empty strings.

The search for `\CHAOS\CDTrack` on the CD drives would have read a one-digit
value from the disc, but nothing calls it, and the music does not use it: MCI
finds the CD audio device by type (FND-AUDIO-007). The leading space in
` A:\CHAOS\CDTrack`, like those in the other path templates, is a placeholder
for the length byte of a counted string.

A player without the disc sees nothing from these functions. The only effect of
a missing disc is that CD music is silent (FND-AUDIO-007).

## Alternatives

- How `GetDriveTypeA` answers `.\` and the other two-character strings on the
  Windows versions of the time has not been checked against the original
  running. On the Windows 11 research machine, with the current directory on a
  fixed drive, it answers 3 for `.\`, so the loop ends on its first pass.
- The digit the CD search reads may have been a disc number for a multi-disc
  release; nothing in the build uses it.

## How to reproduce

List the references to `0x00487B3C`, `0x00487B40` and `0x00487B44`. In
`0x0046638E`, find the call to `GetDriveTypeA`, the compare with 3, the compare
with `Z` (`0x5A`) and the store of 1 into the result. List the callers of
`0x0046638E` and of `0x004667DC`.
