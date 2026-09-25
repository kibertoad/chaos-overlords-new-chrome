---
id: RULE-UI-014
title: Input reaches the screen loops as one polled event at a time, and the event step handles the option commands and window activation for every loop
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-020, FND-UI-021, FND-HELP-005]
conflicting: []
split_with: []
related: [FMT-STATE-009, RULE-UI-008, RULE-UI-007, RULE-HELP-001, RULE-AUDIO-003, RULE-AUDIO-002, SCR-UI-002, SCR-UI-009]
---

## Summary

The window turns Windows messages into an input event of four numbers
(FMT-STATE-009). Every screen loop asks for the next event through one event
step, which waits up to 17 ms for a message, handles the Options, Comm and
About commands and the window's activation itself, and hands the event back.
Loops that animate without taking input only dispatch messages and never see
events.

## When it runs

Each time a screen loop asks for its next event: the title loop, planning,
every panel and picker, the setup screens and the network screens.

## Parameters

None.

## Inputs

`input_event`, `app_deactivated`, `full_screen_active`.

## Procedure

```text
input_event.type = 0
# messages are taken for up to 17 ms, each first offered to accelerator table
# 102; the window procedure writes input_event while it handles one
# (FMT-STATE-009 lists the message for each type), and the wait ends early at
# the first event
# when app_deactivated is set, app_active is set again and the event is not
# type 9, the program posts itself an activation message
if input_event.type == 1:
    if input_event.a == 0x80 and input_event.b == 3:
        show SCR-UI-002
    else if input_event.a == 6:
        music_level = input_event.b - 1
        call RULE-AUDIO-003()
    else if input_event.a == 7:
        effects_level = input_event.b - 1
        call RULE-AUDIO-003()
    else if input_event.a == 0x84 and input_event.b == 1:
        emit DialogShown(136)
        pref_thousands_colors = 1 - pref_thousands_colors
    else if input_event.a == 0x84 and input_event.b == 11:
        emit DialogShown(136)
        pref_full_screen = 1 - pref_full_screen
    else if input_event.a == 0x84 and input_event.b == 6:
        pref_base_stats = 1 - pref_base_stats
    else if input_event.a == 0x84 and input_event.b == 7:
        pref_detailed_combat = 1 - pref_detailed_combat
    else if input_event.a == 0x84 and input_event.b == 8:
        pref_slide_panels = 1 - pref_slide_panels
    else if input_event.a == 0x84 and input_event.b == 9:
        pref_warn_idle = 1 - pref_warn_idle
    else if input_event.a == 0x85 and input_event.b >= 3 and input_event.b <= 6:
        comm_type = input_event.b - 3
    # (0x85, 1) Disconnect runs the network disconnect; (0x80, 1) Help Topics
    # has no branch (RULE-HELP-001)
if input_event.type == 6 or input_event.type == 16:
    # closing or destroying the window acts as File, Exit
    input_event.type = 1
    input_event.a = 0x81
    input_event.b = 9
if input_event.type == 8:
    # CD music stops (RULE-AUDIO-002); in full screen the window is minimized
    app_deactivated = 1
if input_event.type == 9:
    # music restarts, the window is restored and raised, the palette applied,
    # the menu bar attached, the arrow set and the window repainted
    app_deactivated = 0
# the timed work of the presentation clocks runs (RULE-UI-008)
return input_event
```

## Outputs

Returns the event, with the handled commands left in it: a loop that also tests
an Options command sees it after the step has acted. Each Options toggle also
sets or clears its menu check mark, and the Comm choice redoes the Comm menu
(SCR-UI-009). Emits `DialogShown(136)` before either display option is
flipped; the new value takes effect at the next start.

## Edge cases

- Menu commands and their accelerator keys arrive as the same event, so no loop
  can tell them apart. The accelerators match character codes, and Backspace
  and Ctrl+Enter produce the codes of Ctrl+H (Host) and Ctrl+J (Join).
- Keys are translated with a fixed United States shift map: Shift with a digit
  or with `'`, `,`, `.`, `/`, `;` or `=` gives the shifted character, and
  letters always come out in upper case.
- A button press carries the client coordinates of the press. A second press
  counted as a double click becomes type 5 (19 for the right button) only on
  every other double click; a panel that closes resets that phase.
- Loops that animate without input dispatch messages without writing an event,
  and a repaint arriving during them only validates the window.

## What the sources say

None of the sources describes input handling.

## Differences between builds

None known.

## Open questions

- Whether `TranslateAcceleratorA` sends the command of a greyed item needs a
  run of the original; the documented Windows behaviour is that it does not.
