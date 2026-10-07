# Screens against captures of the original

Status: maintained canonical procedure

Part of the [validation procedure](../VALIDATION.md).

A screen entry is compared with the original through a capture of the drawing
area taken at the endpoint of an experiment run. The rebuild replays the same
run, draws its endpoint, and has to draw every listed element of the screen as
the original did.

## Taking a capture

Add `--capture` to the probe's `new-game` command. After the state dump the
probe copies the 640-by-460 drawing area (RULE-GFX-002) from the window's
device context twice with BitBlt, without asking the window to repaint, and
reads the Overlord bar's marker counter `0x00487B90` before and after
(FND-UI-038). It retries until the counter held still and the two copies agree
byte for byte, together with the pump's counter `0x00487804`, then notes the
marker frame the copies show, `(c + 11) mod 12` for a counter value `c`, and
writes `capture-blt.bmp` and
`capture-blt-repeat.bmp`, top-down 32-bit bitmaps, into the run directory. It
also notes the original's display depth `0x0048787C` (FND-PLATFORM-009) and the
depth of its own device context; a capture whose depths differ is recorded as
such. PrintWindow is not used: the timer draws the marker straight to the
window, and a repainting copy loses it.

Add `--white-key` as well (docs/DECISIONS.md, 2026-10-05). On a 32-bit desktop
the original's keyed copies key nothing and draw the white they should leave
out, which is where the solid white areas of Windows 11 come from
(FND-PLATFORM-014). The keyed mask compositor takes its 16-bit key
`RGB(255,252,255)` from an immediate operand (FND-PLATFORM-015), and the option
writes `RGB(255,255,255)`, the white a 32-bit surface holds, over that operand
once, before the game runs. A run with the option stops no more often than one
without it, so the timer records of a run with `--time-limit` are not moved.
When the operand does not hold the expected bytes, the write is skipped and the
run's notes say so. The fixture lists the option as the setup input
`key_colour RGB(255,255,255)`. The rolls and the state of a run do not depend
on it.

`extract --screens SCR-UI-003,SCR-HIRE-002` adds a `capture` object to each
run whose two copies agree:

- `xxh3`: the hash of `capture-blt.bmp`, the spec's xxh3 (`SpecHash`);
- `area`: `[0, 0, 640, 460]`;
- `marker_frame`: the marker frame the capture shows;
- `pump_counter`: the value of the pump's counter `0x00487804`, which held
  still across both copies as well. The pump draws the selected-sector frame
  for its counter and then advances it, so the frame on screen is
  `((n + 7) % 8) / 4` for a counter `n` (FND-UI-017, FND-UI-048);
- `lamps`: the Events light's flag `0x00487814` and the byte `0x00487818` the
  pump sets when it draws that lamp, then the Comlink light's `0x0048781C` and
  `0x00487820` (FND-EVENT-006), read with the copies. Captures taken before
  the probe read them have none;
- `screens`: for each screen named, its elements, each with the element's name
  as the entry's Drawn elements table gives it, its `rect` `[x, y, width,
  height]`, the `xxh3` of the rectangle's pixels and `white`, the number of
  those pixels that are exact white.

The digest of a rectangle is the xxh3 of its pixels as red, green and blue
bytes, row by row from the top and left to right. The elements of a screen and
their rectangles, worked out for the state the capture shows, are in
`tools/Rechaos.OriginalProbe/Screens/<SCR ID>.json`; a screen with no such file
cannot be named yet. Each rectangle comes from the entry's Position column, and
an element whose position the entry does not give is left out of the list.
`ScreenElementListTests` checks every list: it names its screen entry, the
screen has an entry in `ScreenCaptureMasks`, each rectangle lies inside the
640-by-460 drawing area, and each element names a row of the entry's Drawn
elements table, or is named after the entry's title and covers the whole
drawing area. The name is the row's own, or the row's name (or that name's
part before its own comma) followed by a comma and either an index such as
`slot 0` or a field or variant that the row's Element or Shows cell names as a
whole word: `Offer portrait, slot 0` cites the row
`Offer portrait, one per offer slot`, and `Value fields, Gang Upkeep` cites
`Value fields`, whose Shows cell lists Gang Upkeep.

The bitmap holds the game's art, so it never goes into the repository. When
`GAME_DIR` is set, `extract` copies it to `GAME_DIR/captures/<xxh3>`, the
directory `OriginalGameFiles` reads captures from; otherwise it prints where to
copy it. Every file there is named by its xxh3 alone, with no extension, as the
documentation standard names them, and `OriginalGameFiles` refuses a copy kept
under another name such as `<xxh3>.png` instead of skipping the test.

Captures are taken without a DirectDraw wrapper (docs/DECISIONS.md,
2026-10-05). DDrawCompat beside the staged copy left the rolls and the state of
a recorded run unchanged but did not remove the white areas: windowed, the
original draws with GDI and never uses DirectDraw (FND-GFX-004).

A capture can also be taken after the dump, as a step of `--order-steps`:
`shot:SCR-A+SCR-B` copies the drawing area as `--capture` does and keeps
`capture-step-<n>.bmp` and its repeat, where `n` is the step's index, with the
marker frame, the pump's counter, the light bytes (`lamps`), the selected
sector `0x004ABC80` (`selected_sector`) and the frame counter
(`frame_counter`). While a panel that slid in is open the pump draws no
selection frame (FND-UI-051), so the probe breaks at `0x004196E4`, where the
slide-in sets the flag that stops it, and keeps the pump's counter read there;
the frame counter is that value while the flag is set, the pump counter when it
is clear, and null when the probe could not tell. A panel that slides in over
another finds the flag set and leaves the counter as it was. While Item
Information, Sell or Give is open the shot also keeps `item_frame`, the frame
of its rotating items, read from the handler's local (FND-UI-052,
FND-UI-053); while the idle gang warning is open, `idle_phase`, the ticks since
its open modulo 8, read from its countdown and shown flag (FND-UI-054); for a
shot of the Comlink Send panel, `caret_phase`, 3 while its caret is drawn
inverse and 0 while plain, read from the byte at `0x00498110`
(FND-COMLINK-010); and while a Detailed Combat clip plays, `clip_tick`, the
clip's tick (FND-COMBAT-016), and `clip_index`, the clip's index within its
presentation, counted from 0 (FND-COMBAT-011); a shot taken between two clips
keeps neither. Each is read before and after
the capture, which is taken again when the two reads differ, and a value that
moved during every attempt is left out. A `warn` step
switches Warn if Idle Gangs back on for the steps after it. `extract` gives that
order step a `capture` object as above and a `screens` string naming the
screens it is compared at. The steps before it bring the screen up: `open:s`
double-clicks sector `s` on the city map, `dbl:x:y` double-clicks the window
point `(x, y)` as `open` does (FND-UI-020), `strip:x:y:0` presses a point,
`card` a sector card's point, `back` the detailed sector screen's back
control and `exit` the Exit of the panel the planning entry left open.
EXP-UI-006 to EXP-UI-014 are taken this way.

A shot can also show a control while a button holds it. `down:x:y` presses the
left button at `(x, y)` and keeps it down, `move:x:y` moves the pointer there
with the button down, and `up:x:y` releases it there; `rdown:x:y` and
`rup:x:y` press and release the right button. The helpers that hold a button
read the pointer record and the two pointer points of FND-UI-020 (FND-UI-046),
so the probe writes both points itself, as the hire steps do, and posts only
the button messages. The desktop cursor's own moves also reach those points, so
a shot taken while a button is down writes the held point again and waits
0.3 seconds before it copies. Every order step also keeps `slot_zero_clears`,
the milliseconds from the step's start to each tick of timer slot 0 a panel
loop took before the next step began (FND-UI-047), read from breakpoints on the
calls that clear the slot. EXP-UI-041 to EXP-UI-044 are taken with these steps.

`new-game --title-capture` copies the title screen before the run presses New
Game: a breakpoint at the title loop's first load of its art (FND-UI-055)
stops the presses, and the drawing area is copied two seconds later. `extract`
gives the run a `title_capture` object, compared at SCR-UI-001, with marker
frame 0. `--credits-capture` then posts Help, About, copies the credits once
the breakpoint after their load (FND-UI-055) has been hit, and closes them with
the space bar; `extract` gives the run a `credits_capture` object, compared at
SCR-UI-002. `--setup-capture` writes the initialized values of the objective,
Mentality and planning limit options (FND-OPTIONS-001) before New Game, so the
setup screen opens as it does when the registry key holds none, and copies the
setup screen two seconds after it opens, before the run writes its own
settings; `extract` gives the run a `setup_capture` object, compared at
SCR-SETUP-001. `--setup-steps` then posts presses on the setup screen, as
`strip:x:y`, drags as `drag:x:y:x2:y2` and copies as `shot`, each copy
compared at SCR-SETUP-001; `extract` lists them as the run's `setup_steps` and
as `setup` inputs. A drag writes the two pointer points of FND-UI-020 at the
press, at each of eight steps to the release point and before the release, as
the hire steps do, and posts only the button messages. The run's own settings
then replace only what they set: any other choice keeps what the presses left,
and the trace notes which choices the presses changed. EXP-UI-015 is taken this
way, with an earlier drag that posted the moves as `WM_MOUSEMOVE` instead.

Two steps type keys, from tokens `{VKhh}` (a press and release of virtual key
`hh`), `{CHARhh}` (character `hh` posted as `WM_CHAR`), `{SHIFT}` and
`{PLAIN}`. The setup step `name:TOKENS` presses the name band of card 0, which
opens the name editor (dialog 139, FND-UI-064), posts the keys to its edit
control with Shift set in the keyboard state the probe shares with the game's
thread while `{SHIFT}` holds, presses OK and keeps slot 0's 12-byte name
record; `extract` lists the records as `name_entries`. The order step
`keys:TOKENS` posts the keys to the game window and makes the window
procedure's Shift test at `0x0045CA62` report Shift held or not as the tokens
say, since a posted message cannot hold the key; it keeps the type, character
and key the procedure stores at `0x0045CC35` for each, listed as
`key_events`. Num Lock plays no part in either: Windows turns a number-pad key
into a virtual key before it is posted, so a run posts the virtual key each
Num Lock state would give. Both steps note the keyboard layout of the game's
thread, which the translation follows. EXP-UI-052 and EXP-UI-053 are taken
this way.

A capture recorded before the element digests existed, such as those of
EXP-TURN-041 and EXP-TURN-042, gets them from its bitmap under
`GAME_DIR/captures/` without another run of the original:

```powershell
$env:GAME_DIR = 'D:\original-files'
dotnet run --project tools/Rechaos.OriginalProbe -- digest --fixture spec/experiments/<EXP ID>.json --run <n> --screens <SCR ID>,...
```
