---
id: FND-UI-057
title: The gang order popups play no sound, and only the picker panels they open play the panel-open sound
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414D8C..0x004150F1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041462F..0x00414757
tool: Capstone 5.0.7
environment: null
---

## Observation

- In the gang card handler `fn_00414D8C` (FND-UI-021), from its entry to the
  calls that enable the menu items again after the popup (`0x004150F1`), the
  calls are to `fn_00425F4D`, `fn_00425EDF`, `fn_00449B78`, the greying helper
  `fn_0042548A`, the enabling helper `fn_0042533F`, `fn_00425F8C` and the
  popup helper `fn_0042566D`, which calls `TrackPopupMenu` (FND-EXE-005).
- In the group bar handler `fn_0041462F` (FND-UI-021), from its entry to the
  calls that enable the items again (`0x00414757`), the calls are to
  `fn_0042548A`, `fn_0042533F`, `fn_00425F8C` and `fn_0042566D`.
- None of these functions calls the sound wrapper `fn_00464290`
  (FND-AUDIO-002) or the panel-open helper `fn_0041953E` (FND-UI-011);
  `fn_00425F4D` (`0x00425F4D..0x00425F8B`) and `fn_00425F8C`
  (`0x00425F8C..0x00425FAF`) make no call, `fn_00449B78`
  (`0x00449B78..0x00449BD1`) calls one import, and the greying and enabling
  helpers call only imports.
- After the popup, an order with a picker runs it: the Influence picker
  `fn_0043F692` at `0x00415114` in the card handler, and the Attack, Influence
  and Move pickers `fn_0043B290`, `fn_0043F692` and `fn_004413EF` at
  `0x00414801`, `0x00414865` and `0x0041488D` in the group bar handler.

## Interpretation

Opening or dismissing an order popup plays no game sound. With Slide Panels
on, the first sound after the popup is the panel-open helper's slot 0 when the
chosen order's picker panel slides in, as the Move panel does in EXP-UI-025.

## Alternatives

- Windows itself may play a menu sound from the user's sound scheme when a
  popup opens. The default scheme has none, and no run has recorded one.
- The functions `fn_00425F4D`, `fn_00449B78` and `fn_00425F8C` are not
  identified beyond the calls listed here.

## How to reproduce

Disassemble the two ranges above and list their calls; then list the calls of
each function they call, looking for `0x00464290` and `0x0041953E`.
