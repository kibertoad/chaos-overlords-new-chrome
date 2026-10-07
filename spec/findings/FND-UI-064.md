---
id: FND-UI-064
title: The key handler tests Shift once and stores its event at one join, and the name editor's edit control upper-cases what is typed
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045CA43..0x0045CC35
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045CBBB..0x0045CC16
tool: a disassembly of the executable and a resource listing of DIALOG/139
environment: null
---

## Observation

The `WM_KEYDOWN` branch of the window procedure (FND-UI-020) starts at
`0x0045CA43`. It stores 2 in the event type at `0x00498360` and the low 16 bits
of `wParam`, the virtual key, at `0x00498368`. It then calls
`GetAsyncKeyState(VK_SHIFT)` at `0x0045CA5C`; the call returns to
`0x0045CA62`, which tests bit 15 of the result. That is the only Shift test of
the branch.

With Shift not held it jumps to `0x0045CC1C`, which calls
`MapVirtualKeyA(key, 2)` at `0x0045CC27` and stores the result's low 16 bits
in the character at `0x00498364`. With Shift held it calls
`MapVirtualKeyA(key, 2)` at `0x0045CA79` and switches on the result minus
`0x27` through the 23-entry table at `0x0045CBBB`. The sixteen cases
FND-UI-020 lists store their shifted character at `0x00498364`; the other
seven entries, and every result outside `0x27` to `0x3D`, go to `0x0045CB80`,
which calls `MapVirtualKeyA(key, 2)` a third time, at `0x0045CB8B`, and
stores that result. Every path ends at `0x0045CC35`, a jump to the
procedure's common exit, where the event record holds its final type,
character and key.

Dialog 139, the player name editor `fn_0040F63D` opens (FND-UI-022), holds an
edit control with ID 1007 whose style is `0x50830188`: `WS_CHILD`,
`WS_VISIBLE`, `WS_BORDER`, `WS_TABSTOP`, `ES_UPPERCASE` (`0x0008`),
`ES_AUTOHSCROLL` (`0x0080`) and `ES_NOHIDESEL` (`0x0100`). OK has ID 1 and
Cancel ID 2. The dialog runs its own message loop inside `DialogBoxParamA`,
so the keys typed into the name reach the edit control and never pass through
the window procedure's key branch.

## Interpretation

A setup name is typed into a standard Windows edit control. With
`ES_UPPERCASE` the control turns lower-case letters into capitals as they are
typed, so the copy in `fn_0040F63D`, which turns every character outside 32 to
90 into a space, does not see lower-case letters from the keyboard. What
reaches the name is the character Windows produces for the key, which for the
keys outside the letters and the main digits is not given by the key switch
of FND-UI-020.

Comlink text and the other screens that read key events see the character the
window procedure stores at the join. Which character `MapVirtualKeyA(key, 2)`
gives for the number-pad keys is not read here: it comes from the keyboard
layout, not from the executable.

## Alternatives

FND-UI-022's interpretation that lower-case letters become spaces in a name
holds for the copy alone; a lower-case letter could reach it only through a
path that bypasses the control's style, such as pasted text. Whether the
control upper-cases pasted text is not recorded here.

## How to reproduce

Disassemble `0x0045CA43` to `0x0045CC3A` and read the three calls through the
import slot `0x004AE8C4` (`MapVirtualKeyA`) and the call through `0x004AE860`
(`GetAsyncKeyState`). Dump `Chaos Overlords.exe#DIALOG/139` with a resource
reader and read the style of control 1007.
