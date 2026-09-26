---
id: FND-PLATFORM-009
title: The program entry allows one instance, picks the image set, sets up the display, sound and menus, runs the title loop, and undoes it all on the way out
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CCF..0x004622D3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465620..0x00465A94
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465A95..0x00465B26
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004622D4..0x00462522
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462523..0x00462578
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00460CCF` is the program's `WinMain`: its only caller is the C runtime entry
at `0x00478E4B` (FND-EXE-004). Read in order, it does the following.

Instance and window class, in `fn_00465620` (called at `0x00460CF7`):

- It creates a mutex named by the string at `0x00487B08` (the window title),
  owned by the caller, and keeps the handle at `0x00487B38`. When
  `GetLastError` then returns 183 (`ERROR_ALREADY_EXISTS`), it finds the window
  with that title, restores it with `ShowWindow(1)` if it is minimized, brings
  it and its last active popup to the front, and returns 999. `WinMain` then
  returns 999 at once (`0x00460CFF`) and does nothing else.
- Otherwise it reads the command line. When text follows the quoted program
  path, it copies that text to `0x004985D9`, puts a length byte at
  `0x004985D8`, and sets the current directory to the program's directory (the
  path up to its last backslash, starting after the opening quote). When
  nothing follows, it builds the directory from the first character of the
  command line, the opening quote included (`0x0046586C`), and passes that to
  `SetCurrentDirectoryA`.
- It seeds the runtime generator from `timeGetTime` (FND-RNG-001) and calls
  `timeBeginPeriod(17)` (`0x0046590F`).
- It registers the window class at `0x004985A8`: size 48, style `0x28`
  (`CS_OWNDC | CS_DBLCLKS`), window procedure `fn_0045C33B`, icon group 152 as
  both the large and the small icon, the cursor `LoadCursorA(NULL, NULL)`
  (which yields no cursor), stock object 4 (`BLACK_BRUSH`) as background, menu
  resource 101, and the title string as class name. When `RegisterClassExA`
  fails it returns 0, which `WinMain` does not test.
- It loads accelerator table 102 into `0x00487B18` and tries to load menu
  resources 0 to 15 into the sixteen handles at `0x00498870`; of these only 1,
  2, 3 and 5 exist (FND-EXE-005). It clears the pointer state at
  `0x00498598..0x004985A7` (FND-UI-020).

Image set and display, in `WinMain`:

- The preferences are read by `fn_0046439A` (FND-OPTIONS-001).
- It opens ` data\PX08\px00128` and ` data\PX16\px00128` through the file layer
  `fn_0042B60F` (FND-PLATFORM-002) and remembers which opened. With
  `pref_thousands_colors` 0 it asks for depth 8 when the 8-bit file opened,
  else depth 16 (setting `pref_thousands_colors` to 1) when the 16-bit file
  opened. With `pref_thousands_colors` nonzero the preference order is 16, then
  8 (setting it to 0). When neither file opened it beeps and asks for depth 0.
- It calls the display setup `fn_00425850` with the size 640 by 480, the depth
  asked for and the show command it received (FND-GFX-004); the depth returned
  goes to `0x0048787C`.
- When neither file opened it beeps and shows dialog 135 (`0x00460EA1`). When
  the depth returned is 8 but the 8-bit file did not open, or 16 but the 16-bit
  file did not open, it beeps, shows dialog 20007, sets `pref_full_screen` (and
  for 8 also `pref_thousands_colors`) to 1, writes the preferences with
  `fn_00464783` and skips the game. Any other depth, 0 included, also skips the
  game.

Subsystems, in this order from `0x00460F87`: the palette of the window
surface `fn_004282AA(0, 2)` (FND-PLATFORM-007); sound setup `fn_00458290(1)`;
the three movie slots `fn_0040DAE0`; the file layer `fn_0042AB80`; the menu
bar table `fn_004252D0` and `fn_00425319` for the groups `0x81`, `0x82`,
`0x84`, `0x85` and `0x80` (FND-UI-021); the network state `fn_004211E0`;
`DrawMenuBar`; the CD and wave volumes read by `fn_00458ACC` and
`fn_00458E2F` and kept on the stack; `fn_00464385`; the check marks of Base
Statistics, Detailed Combat, Slide Panels, Warn if Idle Gangs and Full Screen;
the sound levels `fn_004652A0`; the Comm menu `fn_00465193` and
`fn_004650E2`. Thousands of Colors is enabled only when both probe files opened
and `full_screen_active` is set, and is checked when the depth is not 8; the
same test sets `pref_thousands_colors` (`0x0046112C`).

Surfaces, in `fn_004622D4`: it creates the memory surfaces 1 (640 by 460),
2 (432 by 832), 3 (640 by 576), 5 (140 by 1408), 6 (512 by 646), 7 (720 by
416) and 11 (720 by 480) at the display depth (FND-GFX-004), and returns 0 when
any of them has no device context. `WinMain` then beeps and shows dialog 132.

When the surfaces exist and the CD test `fn_0046638E` returns nonzero
(`0x00461165`), the title part runs:

- timer slot 0 at rate 6 and slot 1 at rate 10 (FND-TIMER-002), and the sound
  effect slots of FND-AUDIO-002;
- the pointer hidden, the rectangle top 0, left 1, bottom 209, right 345 of
  surface 1 filled black, the one-second copy benchmark `fn_00432954` from
  surface 1 to the window over that rectangle (its count goes to
  `0x004981F8`), the pointer shown again and set to the arrow;
- six player colours built at `0x004ABC18..0x004ABC3B`, the default player names
  `fn_00410016`, `0x00498350` and `0x004ABC9C` set to 1;
- the intro `fn_004329C0` when `loaded_game_kind` (`0x0048788C`) is 0;
- the window's 640-by-460 area filled black, the palette applied again,
  `PX00130` loaded into surface 1, music track 0 started with `fn_004642BD(0)`,
  `PX00129` loaded into surface 6, and surface 1 copied to the window.

The title loop (`0x004616D2..0x00462242`) takes one event at a time from
`fn_00462579` until `quit_requested` (`0x00487828`) is set:

- A left press (event 3) is turned into the command `(0x81, 1)` before the
  command tests, so a press anywhere on the title acts as New Game.
- `(0x81, 1)` New Game greys the Comm menu, runs setup `fn_0040E0A0`, and when it
  returns nonzero sets `in_game` (`0x00487830`), loads string 11 as the game's
  name, enables Save through `fn_00464AE6(0)` and enters planning
  `fn_0046E766(1)`.
- `(0x81, 2)` Open calls `fn_004637B8`; a result of 1, 2 or 3 is kept in
  `loaded_game_kind` and handled after the event.
- `(0x81, 6)` Host greys the transports and calls `fn_004677F0` with
  `comm_type`, then string 12 as name and `fn_0046E766(1)`; `(0x81, 7)` Join
  calls `fn_0040B9C0` and then `fn_0046E766(0)` with Save disabled.
- `(0x81, 9)` Exit sets `quit_requested`. Events 6 and 16 (window closed or
  destroyed) arrive already turned into `(0x81, 9)` by `fn_00462579`.
- Event 7 (a repaint) copies surface 1 to the window between `BeginPaint` and
  `EndPaint`.
- After each game the loop reloads `PX00130`, redraws the title and restarts
  track 0 unless `quit_requested` is set. `loaded_game_kind` 1 enters planning
  `fn_0046E766(0)`, 2 runs `fn_00456F80` before planning, and 3 calls
  `fn_004215BB(4)`, sets `0x00482178`, calls `fn_0040DAB9` (which always
  returns 0) and sets `quit_requested`.

Shutdown: after the loop `WinMain` writes the preferences (`fn_00464783`),
calls `fn_00464385`, puts back the CD and wave volumes it read at startup,
kills timers 0 and 1 and releases the surfaces with `fn_00462523` (surfaces 1,
2, 3, 5, 6, 7 and 11). On every path, including the failures above, it then
calls `fn_0042572C(1)` (menu bar attached), `fn_00426202(0)` (which does
nothing for surface 0), `fn_0046658D` (returns 0), `fn_0042ABFB`,
`fn_004214BA`, `fn_004584BA`, the display undo `fn_00425D97` (FND-GFX-004) and
`fn_00465A95`. The last releases the mutex, calls `timeEndPeriod(17)`,
destroys the menus it loaded and the attached menu, and releases the window's
device context.

## Interpretation

Only one copy of the game runs; a second start brings the first to the front.
A saved game can be passed on the command line (a file association or a drop
on the program), and the window procedure opens it when the window is created
(FND-UI-020); such a start skips the intro. Without an argument the attempt to
set the working directory is given a path with a leading quote and fails, so
the game relies on the directory it was started in. The title screen has no
buttons: a left click anywhere starts a new game, and every other action is a
menu command. When the display cannot give the depth of an installed image
set, the game changes the preferences to full screen at 16 bits and exits,
and the next start tries again.

## Alternatives

Whether `SetCurrentDirectoryA` accepts a path with a leading quote on some
Windows version has not been tried. The handling of `loaded_game_kind` 3 (the
`M10W` file of FND-SAVE-001) ends the program; that is read from the code and
has not been observed.

## How to reproduce

Follow the runtime entry's call at `0x00478E4B` into `0x00460CCF`. The first
call there is `0x00465620`; `CreateMutexA` is at `0x00465671`. The probes are
at `0x00460D22` and `0x00460D4E`, the display setup call at `0x00460E76`, the
surface setup at `0x0046112A` and the title loop's event call at `0x004616D2`.
