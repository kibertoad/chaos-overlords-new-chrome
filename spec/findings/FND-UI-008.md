---
id: FND-UI-008
title: Menu resource 101 defines the File, Options, Comm and Help menus and their command IDs
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462579
tool: Ghidra 12.1.3
environment: null
---

## Observation

The menu resource `Chaos Overlords.exe#MENU/101` holds the application menu.
The dispatcher `fn_00462579` splits each 16-bit command ID into its high and
low bytes, so `0x8003` reaches its `(0x80, 3)` branch. The menu holds:

- File: New Game `0x8101`, Open `0x8102`, Save `0x8103`, End `0x8104`, Host
  `0x8106`, Join `0x8107` and Exit `0x8109`.
- Options: Thousands of Colors `0x8401`, Full Screen `0x840B`, eleven Music
  levels `0x0601` to `0x060B`, eleven Sound Effects levels `0x0701` to
  `0x070B`, Base Statistics `0x8406`, Detailed Combat `0x8407`, Slide Panels
  `0x8408` and Warn if Idle Gangs `0x8409`.
- Comm: Disconnect `0x8501`, and the transports None `0x8503`, WinSock
  `0x8504`, Modem `0x8505` and Direct Connect `0x8506`.
- Help: Help Topics `0x8001` and About Chaos Overlords `0x8003`.

## Interpretation

The original window has an ordinary Windows menu bar above its 640-by-460
drawing area. New game, loading, saving, the options, the network transports,
help and the credits are menu commands, not buttons on the title screen. The
Music and Sound Effects menus pick one of the eleven levels 0 to 10 of
FND-AUDIO-001 and FND-AUDIO-002.

## Alternatives

- The mapping of the level commands to levels (`0x0601` to level 0 or to level
  10) has not been read.
- The manual says the menu bar is hidden during play and appears when the
  pointer reaches the top of the screen; that behaviour has not been found in
  the executable.

## How to reproduce

Extract `Chaos Overlords.exe#MENU/101` with any resource viewer and read the
item IDs. In `0x00462579`, find the dispatch on the high byte of the command ID.
