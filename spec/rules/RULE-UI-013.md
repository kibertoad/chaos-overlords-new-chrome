---
id: RULE-UI-013
title: The program starts one instance, chooses the image set and display depth, runs the title loop, and undoes its setup on the way out
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-PLATFORM-009, FND-UI-020, FND-GFX-004, FND-TIMER-002, FND-UI-022, FND-VIDEO-002, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-OPTIONS-001, RULE-OPTIONS-002, RULE-GFX-002, RULE-UI-014, RULE-UI-008, SCR-UI-001, SCR-UI-009]
---

## Summary

At start the program refuses a second copy, reads the options, picks the 8-bit
or 16-bit image set, sets up the display and the other subsystems, and shows
the title screen. The title loop turns a click or a File command into a new,
loaded or network game and comes back to the title after each game. On the way
out it writes the options and puts the display back.

## When it runs

Once, when the program is started.

## Parameters

None.

## Inputs

`instance_running`, `image_set_present`, `pref_thousands_colors`,
`pref_full_screen`, `loaded_game_kind`, `quit_requested`.

## Procedure

```text
if instance_running != 0:
    # the running copy's window is restored if minimized and brought to the front
    return 999
# the command line's text after the program path is kept as a file to open;
# the multimedia timer period is set to 17 ms for the whole run
call RULE-OPTIONS-001()
let want = 0
if pref_thousands_colors == 0:
    if image_set_present(8) != 0:
        want = 8
    else if image_set_present(16) != 0:
        want = 16
        pref_thousands_colors = 1
else:
    if image_set_present(16) != 0:
        want = 16
    else if image_set_present(8) != 0:
        want = 8
        pref_thousands_colors = 0
# the display setup creates the window, which opens the command-line file and
# sets loaded_game_kind from it, and sets display_depth
call RULE-GFX-002(want)
let ready = 0
if want == 0:
    emit DialogShown(135)
else if display_depth == 8 and image_set_present(8) == 0 or display_depth == 16 and image_set_present(16) == 0:
    emit DialogShown(20007)
    pref_full_screen = 1
    if display_depth == 8:
        pref_thousands_colors = 1
    call RULE-OPTIONS-002()
else if display_depth == 8 or display_depth == 16:
    # palette, sound, movies, files, menu bar groups, network state, check marks
    if surfaces_created() == 0:
        emit DialogShown(132)
    else if cd_present() != 0:
        ready = 1
if ready != 0:
    # timer slots 0 and 1 start (RULE-UI-008); the copy benchmark runs for one
    # second; the default player names are built
    if loaded_game_kind == 0:
        emit IntroPlayed()
    # the title art is drawn and title music starts (SCR-UI-001)
    while quit_requested == 0:
        let ev = call RULE-UI-014()
        if ev.type == 3:
            # a left press anywhere acts as New Game
            ev.type = 1
            ev.a = 0x81
            ev.b = 1
        # (0x81, 1) new game: setup, then planning
        # (0x81, 2) open: loaded_game_kind = the kind of file loaded
        # (0x81, 6) host, (0x81, 7) join: network setup, then planning
        # (0x81, 9) exit, and a closed window: quit_requested = 1
        # loaded_game_kind 1: planning of the loaded game; 2: network resume;
        # 3: the network shutdown, then quit_requested = 1
    call RULE-OPTIONS-002()
    # the CD and wave volumes read at start are put back, timers 0 and 1 are
    # killed and the surfaces released
# on every path: menu bar attached, sound, file and network layers closed, the
# display put back (RULE-GFX-002), the mutex released, the timer period ended
return 0
```

## Outputs

The program's exit code: 999 when another copy runs, 0 otherwise. Emits
`DialogShown` with the dialog number when the program cannot use the
display (132, 135) or the depth it got does not match an installed image set
(20007); in the last case the options are rewritten to full screen before the
program exits, so the next start asks for full screen. Emits `IntroPlayed`
when the intro movies (FND-VIDEO-002) are played, which is skipped when a game
was loaded from the command line.

## Edge cases

- A file named on the command line is opened while the window is created, so
  `loaded_game_kind` is set before the intro test and such a start skips the
  intro and goes straight to the loaded game after the title is drawn.
- A depth of 0 from the display setup, which happens in full screen when
  DirectDraw cannot be created, ends the program without a dialog.
- The options are written only here, at exit and after the depth mismatch; a
  change in the Options menu is kept in memory until the program ends.
- `cd_present` returning 0 also ends the program; what the CD check shows is
  described with the CD drive search.

## What the sources say

None of the sources describes the start sequence.

## Differences between builds

None known.

## Open questions

- Whether `SetCurrentDirectoryA` succeeds with the leading quote the program
  passes when started without an argument needs a run of the original.
- The handling of `loaded_game_kind` 3 is read from the code only.
