---
id: FND-PLATFORM-010
title: Data files are named by the App Paths install directory and length-prefixed names, and four file slots open them with no message on failure
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042B8EC..0x0042B9D8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004876B4..0x00487700
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487538..0x0048763F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449BFC..0x00449C8D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042AB80..0x0042AF30
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042B60F..0x0042B8EB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00493F88..0x004944FF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004667DC..0x0046690F
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

Install directory. `fn_0042B8EC` opens the key
`SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Chaos Overlords.exe`
(string at `0x004876B4`) under `HKEY_LOCAL_MACHINE` with access `0x20019`
(`KEY_READ`) and reads its value `Path` (`0x004876FC`) into the 260-byte
buffer at `0x00487538`. It then counts the characters into the length at
`0x0048763C`, stopping at a NUL or at 260, and appends a backslash when the
last character is not one. When the key or the value is missing nothing is
stored and the length stays 0. Its three callers, the sound setup
`fn_00458290`, the image loader `fn_00464155` and the sound loader
`fn_004589B8`, call it only while the length is 0, so the registry is read
again at every one of their calls until a value is found. `fn_00464155` passes
the buffer to `SetCurrentDirectoryA` right after (`0x004641A0`), and both file
dialogs pass it again after they close (FND-SAVE-002). The value is read with
`RegQueryValueExA` and no type check.

Names. The file layer takes names as length-prefixed strings: byte 0 holds
the length and the characters follow. Callers build them with the byte copy
`fn_00449BFC` (source, destination, count) and finish them with
`fn_00449C41`, which counts the characters from byte 1 up to the first NUL
and writes the count into byte 0. Its counter is a signed byte
(`0x00449C52..0x00449C71`): for a name of more than 126 characters it turns
negative and the count goes wrong, and `fn_0042B60F`, which reads the length
byte as signed (`0x0042B642`), then copies no characters and the open fails. The table, sound and palette
names are the install directory followed by the file name
(`0x0046E8C1..0x0046EA0D`, FND-DATA-007). The image names are the directory
followed by `data\PX08\PX00000` or `data\PX16\PX00000` with the digits
replaced (FND-PLATFORM-002), passed on as ordinary NUL-terminated strings to
`CreateFileA`.

The strings with a leading space are templates of the length-prefixed form.
` data\PX08\px00128` (`0x0048789C`) and ` data\PX16\px00128` (`0x004878B0`)
are passed straight to `fn_00449C41` (`0x00460D22`, `0x00460D4E`), which
overwrites the space with the length 17 before `fn_0042B60F` sees them; these
two names carry no install directory. ` A:\CHAOS\CDTrack` (`0x00487B44`) is
copied to the stack by `fn_004667DC`, which puts each drive letter from C to Z
that `GetLogicalDrives` lists and `fn_0046678D` finds to be a CD-ROM drive
(`GetDriveTypeA` returns 5 for the root built from `A:\` at `0x00487B40`) in
place of `A`, finishes the name with `fn_00449C41`, opens it through slot 1,
reads one byte and returns it minus `'0'`. No instruction calls
`fn_004667DC` and no four-byte value in the file equals its address. In none
of the three strings does the space reach a file name.

File slots. The layer keeps four slot records of `0x15C` bytes from
`0x00493F90`:

| Offset | Size | Contents |
|---|---|---|
| `0x00` | 1 | Set when the slot has a name that was found or chosen |
| `0x04` | 4 | The handle |
| `0x08` | `0x4C` | An `OPENFILENAMEA` for the dialogs (FND-SAVE-002) |
| `0x56` | 1 | Set while the handle is open |
| `0x57` | 261 | The name, NUL-terminated; the dialogs' file buffer |

The dword at `0x00493F88` is written once, with `0x736D434F` by
`fn_0042AB80`, and never read.

- `fn_0042AB80` (called once from `WinMain` at `0x00460FA3`) clears the four
  name flags, puts the two bytes at `0x00487640` (a space and a NUL) at the
  start of each name, and stores its argument at `0x00493F88`.
- `fn_0042B60F(slot, name, unused, create)` accepts slots 0 to 3. It copies
  the length-prefixed name into the slot and opens it: without `create` for
  reading with read sharing and `OPEN_EXISTING`; with `create` for reading and
  writing with read and write sharing and `OPEN_ALWAYS`. When the handle is
  valid it closes it at once, sets the name flag and returns 1; otherwise it
  returns 0 and leaves the flag as it was. The third argument is not read.
- `fn_0042AC7A(slot)` opens a named, closed slot for reading and writing with
  read and write sharing and `OPEN_EXISTING`, and when that fails for reading
  only with read sharing. It sets the open flag when either works and reports
  nothing when both fail.
- `fn_0042AE85` and `fn_0042AF31` call `ReadFile` and `WriteFile` on an open
  slot and return the byte count, 0 for a slot that is not open. No caller
  tests the count.
- `fn_0042ADE9` closes an open slot; `fn_0042ABFB`, called from `WinMain` at
  `0x004622AD` on the way out, closes every slot still open.
- `fn_0042B7F3(slot, dest)` copies 250 bytes of the slot's name into `dest`
  after its first byte and sets the length byte; the save and load functions
  use it to keep the chosen name at `0x00498200`.
- `fn_0042B87B(slot)` returns the handle of a named slot, 0 otherwise.

Callers of `fn_0042B60F`: `WinMain` for the two image probes, the sound loader
`fn_0045867C`, `fn_0046E766` for the three definition tables, the window
procedure `fn_0045C33B` for a file named on the command line (the
length-prefixed string at `0x004985D8`, FND-PLATFORM-009), and `fn_004667DC`.
The saves go through the dialog functions instead (FND-SAVE-002).

What a missing file does. `fn_0046E766` skips the read of a table whose file
cannot be opened (FND-DATA-007). `fn_0045867C` skips the sound and leaves its
slot empty. An image that cannot be opened makes `fn_00464108` beep and show
dialog 137, and drawing goes on (FND-GFX-005). The palette loader does not
test its handle (FND-PLATFORM-011). None of these stops the game; only the two
probes at startup do (FND-PLATFORM-009).

## Interpretation

The game finds its files through the installer's App Paths entry and falls back
to the current directory when that entry is missing. An install directory
long enough to push a table or sound name past 126 characters would make those
files fail to open without a message. The leading spaces are
room for the length byte. `CDTrack` is the remains of a check for a file on
the game CD that no code performs. The layer's design, a type code per file
and length-prefixed names, looks carried over from a Macintosh version.

## Alternatives

Where `0x736D434F` would have been used is not known; nothing reads it.

## How to reproduce

Follow the references to `0x00487538` and `0x0048763C` into `fn_0042B8EC` and
its three callers. Follow the references to `0x00493F90` to the file layer
functions, and the references to the strings at `0x0048789C`, `0x004878B0`
and `0x00487B44`.
