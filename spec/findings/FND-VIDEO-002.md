---
id: FND-VIDEO-002
title: The intro plays MVLOGOS then MVINTRO at (80,102) through smackw32, each ended by the left button at a 10 Hz tick, at the effects volume
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004329C0..0x00432D9F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040DAB9..0x0040DAD4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040DAE0..0x0040DBBF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040DBC0..0x0040DD7A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040DD7B..0x0040DDFE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040DDFF..0x0040DFFD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040DFFE..0x0040E091
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004905C0..0x0049062B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00490598..0x004905B7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048770C..0x0048772B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004614B6..0x004614C3
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. The `Smack*` names are the imports
of `smackw32.dll` (FND-PLATFORM-006).

- The title initialization calls the intro `fn_004329C0` at `0x004614C3` only
  when the dword at `0x0048788C` is 0. Before that call only the window
  procedure writes it, at `0x0045C6CB`, with the result of the load function
  `fn_0046381A` when a file named on the command line opens. The title's load
  dispatcher writes 1, 2 or 3 there later (`0x00461917`, `0x0046192D`,
  `0x00461943`), after the intro has run. Timer slot 1 was started at
  `0x0046119B` with a rate of 10, a period of 100 ms (FND-UI-023).
- The intro fills the rectangle with corners `(0,0)-(640,460)` with RGB 0,
  raises the thread priority to 2 (`THREAD_PRIORITY_HIGHEST`), and plays two
  movies in turn. For each it fills the rectangle `(80,102)-(560,358)` with RGB
  0, copies the 13-byte name to `0x00498762`, opens the movie at `0x00498760` in
  movie slot 0 of the window slot 0 with no loop, and sets its volume to
  `effects_level * 25`. The first name is `Data\mvLogos` from `0x0048770C`, the
  second `Data\mvIntro` from `0x0048771C`. The prefix at `0x00498760` is the
  one the disc check left (FND-PLATFORM-012).
- The playing loop of each movie calls the frame helper `fn_0040DDFF`, handles
  one window message through `fn_0045C2CD`, and, when timer slot 1 is set,
  clears it and reads the input state through `fn_00465B64`. When the byte at
  offset 12 of that state is set, it closes the movie. The loop ends when movie
  slot 0 is no longer active (`fn_0040E00E`). The byte at offset 12 is
  `0x004985A4`, which the window procedure sets on `WM_LBUTTONDOWN` and clears on
  `WM_LBUTTONUP`. No key and no other button is tested.
- After the second movie the intro fills the movie rectangle with RGB 0 again
  and sets the thread priority back to 0.
- The movie table at `0x004905C0` has three slots of `0x24` bytes: the active
  byte at 0, the window slot at 4, the loop byte at 8, the frame count at `0xC`,
  the `Smack` handle at `0x10`, the `SmackBuf` handle at `0x14`, the top and left
  of the destination at `0x18` and `0x1A`, the bottom and right at `0x1C` and
  `0x1E`, and the volume at `0x20`. The table ends at `0x0049062B`.
- The table initializer `fn_0040DAE0`, called at `0x00460F99`, clears the three
  slots and sets each volume to `effects_level * 12`.
- The open helper `fn_0040DBC0` calls `SmackOpen(name, 0xFE000, -1)`. A null
  handle leaves the slot inactive and ends the helper. Otherwise it marks the
  slot active, stores the arguments, opens a `SmackBuffer` for the window slot's
  window with blit type 3 and the movie's width and height, goes to frame 0,
  sets the volume to `effects_level * 12`, turns the sound on, and points
  `SmackToBuffer` at the buffer.
- The frame helper `fn_0040DDFF` visits the three slots. For an active slot
  whose `SmackWait` returns 0 it focuses the buffer; on the first frame, or when
  the movie's new-palette field at offset `0x68` is set, it calls
  `SmackBufferNewPalette` with the movie's palette and `SmackColorRemap` with the
  buffer's palette; then `SmackDoFrame`, `SmackToBufferRect`, and
  `SmackBufferBlit` to the window slot's device context at x from offset `0x1A`
  and y from offset `0x18`, for the changed rectangle the movie reports. When
  the frame count equals the movie's frame count, a looping slot goes back to
  frame 0 and a non-looping one is closed. Otherwise it calls `SmackNextFrame`
  and counts the frame.
- The close helper `fn_0040DD7B` clears the active byte, turns the sound off and
  closes the buffer and the movie.
- The volume helper `fn_0040E049` calls `SmackVolumePan` for all tracks
  (`0xFE000`) with the volume shifted left by 8 and the pan `0x8000`, and stores
  the unshifted value. `fn_0040E02B`, which returns the stored volume, and the
  empty `fn_0040DFFE` have no callers.
- `fn_0040DAB9` writes 0 to `0x0048FB70`, a byte of the Join setup
  `fn_0040B9C0`, and returns 0. Its only call, at `0x004621E7`, is on the title
  path for a loaded game of type 3.
- The globals from `0x00490598` to `0x004905B7` belong to the network setup:
  two three-byte tables at `0x00490598` and `0x0049059B`, a flag at
  `0x004905A0`, a count at `0x004905A4` and four seat entries from `0x004905A8`,
  used by `fn_0040B9C0`, `fn_0040C4C5`, `fn_0046913D`, `fn_004677F0`,
  `fn_0040CED0` and `fn_0046BA84`. No Smacker helper reads them, and no network
  function reads the movie table.

## Interpretation

At startup, unless the game was started with a saved game that loads, the logos movie and
then the intro movie play in a 480-by-256 frame centred horizontally near the
top of a black screen, with their sound at the Sound Effects level: silent at
level 0, and `level * 6400` against the library's volume scale. Holding the left
button when a 100 ms tick comes ends the movie playing; a click released
between two ticks is missed, and keys do nothing. Each movie is shown to its
last frame and one step more, then closed. Frames are remapped to the palette of
the Smacker buffer whenever the movie changes its palette; in the 8-bit display
set that is how the movie's colours are fitted to the screen palette.

A movie that is missing or does not open is skipped: its black rectangle is
drawn and the loop ends on its first pass. The intro raises the thread priority
while it plays so that decoding keeps pace.

The globals just below the movie table share nothing with it but their place in
memory.

## Alternatives

- What blit type 3 selects inside `smackw32.dll`, and how the library treats a
  volume above its normal level, is not settled by the executable.
- Whether a movie file of another format fails `SmackOpen` or plays wrongly
  depends on the library.

## How to reproduce

Find the call to `0x004329C0` at `0x004614C3` and the test of `0x0048788C`
before it. In `0x004329C0`, find the two copies from `0x0048770C` and
`0x0048771C` to `0x00498762`, the rectangle constants `0x66`, `0x50`, `0x166`
and `0x230`, the calls to `0x004328BE` with 1, and the calls to
`SetThreadPriority`. In `0x0040DBC0`, find the constant `0xFE000` passed to
`SmackOpen`. List the references to `0x00490598..0x004905BF` and to
`0x004905C0..0x0049062B`.
