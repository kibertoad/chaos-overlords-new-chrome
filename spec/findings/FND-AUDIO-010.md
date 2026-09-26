---
id: FND-AUDIO-010
title: Slot 2 is the sound of pressing a push-button control, and setup plays slots 3 and 4 for accepted and refused choices
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040B9C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040E0A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040CBA5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040EB5F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464108
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00419022
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004718EE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418821
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00416C75
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00439F7A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042CB95
tool: Ghidra 12.1.3
environment: null
---

## Observation

- In the compact player-setup handler `fn_0040B9C0` and the full local-setup
  handler `fn_0040E0A0`, an accepted selector-arrow input plays slot 3 and a
  refused one plays slot 4. The image-loader wrapper `fn_00464108` is called at
  `0x0040BC2E` with `PX00145` and at `0x0040E150` with `PX00143`. The separate
  handlers `fn_004677F0` and `fn_00456F80` load `PX00144` and `PX00146`.
- The four-case push-button helpers `fn_0040CBA5` (compact setup) and
  `fn_0040EB5F` (full setup) draw a pressed button image, play slot 2 once,
  follow whether the pointer stays inside while the left button is held,
  restore the released image when it leaves, and return whether the button was
  released inside. Their destinations are Add Player `(370,328)-(462,352)`,
  Remove Player `(468,328)-(560,352)`, Start `(370,375)-(462,420)` and Back
  `(468,375)-(560,420)`, as half-open corners. When Add or Remove cannot change
  the number of players, the caller also plays slot 4 after the slot-2 sound.
- Every direct call that passes slot 2 to the wrapper `fn_00464290` belongs to
  a pressed-control helper:
  - the main-console helper `fn_00419022`, for each of its eight cases, which
    the dispatcher `fn_004718EE` uses for the Events, Comlink, Combat, Finance,
    Gangs/Hire, Ranking/Search, Done and Game Info tiles (FND-UI-032);
  - the shared pointer helper `fn_00418821`, which plays slot 3 except in its
    case 2, where it plays slot 2. Its only case-2 callers are the two Hire
    rejection paths in `fn_00416C75`, and a successful rejection plays no slot
    3 after it;
  - `fn_00439F7A`, called by the `PX00132` handoff handler for Ready;
  - `fn_0042CB95`, called by the endgame and awards screens for their three
    controls;
  - the compact and full setup helpers above and the setup flows of `PX00144`
    and `PX00146`, at `0x00438DA5`, `0x00468E12` and `0x00457EED`.
- That makes nine direct slot-2 calls in all. A nearby call at `0x0045286C` in
  Combat Results passes slot 3.

## Interpretation

`SND00202`, in slot 2, is the sound of pressing a push-button control with the
pointer. It plays when the button is pressed, before the game knows whether the
button will be released inside. Setup uses slot 3 when an arrow changes a
choice and slot 4 when the choice cannot change. The four resources `PX00143`,
`PX00144`, `PX00145` and `PX00146` belong to four separate setup flows.

## Alternatives

None known.

## How to reproduce

List the callers of the wrapper at `0x00464290` whose pushed slot is the
constant 2. Each lies in one of the helpers listed. In `0x0040EB5F`, the four
destination rectangles are passed to the rectangle helper as constants.
