---
id: FND-UI-068
title: The player name dialog holds an edit control with no text limit, and the game reads its text at every notification and keeps ten characters
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465EC6..0x0046638E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040F63D..0x0040F72E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004B23F8..0x004B24ED
tool: a resource listing of DIALOG/139 and a disassembly of the executable
environment: null
---

## Observation

`Chaos Overlords.exe#DIALOG/139` (language 1033, 246 bytes at `0x004B23F8`) is
a dialog template with style `0x900002C0`: `WS_POPUP`, `WS_VISIBLE`,
`DS_SETFONT`, `DS_MODALFRAME` and `DS_SETFOREGROUND`, with no `WS_CAPTION`, no
menu, no class and an empty title. Its rectangle is `(110, 100, 200, 70)` in
dialog units and its font is MS Sans Serif at 8 points. It holds four items, in
this order:

| ID | Class | Style | Rectangle (dialog units) | Text |
|---|---|---|---|---|
| 1007 | Edit | `0x50830188`: `WS_CHILD`, `WS_VISIBLE`, `WS_BORDER`, `WS_GROUP`, `WS_TABSTOP`, `ES_LEFT`, `ES_UPPERCASE`, `ES_AUTOHSCROLL`, `ES_NOHIDESEL` | `(8, 20, 179, 13)` | Empty |
| 1 | Button | `0x50010001`: `WS_CHILD`, `WS_VISIBLE`, `WS_TABSTOP`, `BS_DEFPUSHBUTTON` | `(142, 47, 50, 14)` | 2 characters |
| 2 | Button | `0x50010000`: `WS_CHILD`, `WS_VISIBLE`, `WS_TABSTOP`, `BS_PUSHBUTTON` | `(84, 47, 50, 14)` | 6 characters |
| -1 | Static | `0x50020000`: `WS_CHILD`, `WS_VISIBLE`, `WS_GROUP`, `SS_LEFT` | `(8, 8, 179, 11)` | 32 characters |

`fn_0040F63D` opens it through `fn_00465CEC(139)` (FND-UI-022), which passes 0
as the initialization value. The dialog procedure `fn_00465EC6` handles
`WM_INITDIALOG` by calling `SetFocus` on the dialog at `0x00465EE4`; none of
the initialization values 141, 20000, 20002 and 200 it tests is 0, so it then
returns 0 at `0x00466225`.

The procedure sends messages to its controls only through
`SendDlgItemMessageA` (import slot `0x004AE894`): directly at `0x0046627F`
and `0x004662C6` (`0x188` to control 1003) and through the wrapper
`fn_00465E71`, whose seven calls send `WM_SETREDRAW` (`0xB`) and `EM_SETSEL`
(`0xB1`) to control 1007 on the path for initialization value 20000, and
`WM_SETREDRAW` and list-box messages to control 1003 on the path for 20002.
The executable does not import `SendMessageA`, and neither its calls to
`SendDlgItemMessageA` nor those to `PostMessageA` (slot `0x004AE8B0`) send
`EM_LIMITTEXT` (`0xC5`): the nine instructions in the code that push `0xC5`
pass it among other small constants to `fn_00425EDF` or `fn_00425F8C`.

On `WM_COMMAND` the procedure ends the dialog with the control ID as result
when the ID is below 8 (`0x0046622C`). Otherwise, unless the ID is `0x3F4` or
the notification code is 2, it reaches `0x004662E4`, which calls
`GetDlgItemTextA(dialog, 1007, 0x00498470, 254)` and then the same for control
1008 into `0x00498370`. An edit control's notifications reach this path, so
the text of control 1007 is copied to `0x00498470` each time the control
reports a change, a gain or a loss of focus.

`fn_0040F63D` clears the first byte of `0x00498470` before it opens the
dialog. After it closes, when the result is 1 and that byte is not zero, it
copies characters from `0x00498470` into the name at
`0x004A2588 + 12 * player`, one per pass, until it has copied ten or reaches the
terminating zero. Each character is loaded with `movsx`; a value below `0x20`
or above `0x5A` is stored as a space. The count goes in the record's first byte
and a zero after the last character.

## Interpretation

The player name is typed into a standard single-line edit control in a modal
dialog without a title bar. The game sets no text limit, so the control keeps
the limit an edit control has before `EM_LIMITTEXT` (SRC-WIN32-EM-LIMITTEXT),
and with `ES_AUTOHSCROLL` it scrolls instead of refusing text wider than its
box (SRC-WIN32-EDIT). A player can therefore type more than ten characters
and edit anywhere in them; OK keeps the first ten characters of what the
control holds, the last change having been copied out by its notification.
Bytes from `0x80` up read as negative and become spaces, as do `[`, `\`, `]`,
`^`, `_`, `` ` ``, `{`, `|`, `}` and `~`. With `ES_UPPERCASE` lower-case
letters are capitals before they reach the copy.

The procedure gives the edit control no focus of its own: it focuses the
dialog and returns 0, and which control then holds the focus, and whether its
text is selected, is decided by the dialog manager, not by the game.

## Alternatives

A notification code of 2 skips the copy; no edit control notification has that
code, so it only concerns list box 1003 of dialog 20002. If a keystroke changed
the text without the control sending a notification, OK would keep an older
copy; the control's documented behaviour is to notify each change
(SRC-WIN32-EDIT).

## How to reproduce

Dump `Chaos Overlords.exe#DIALOG/139` with a resource reader and decode the
`DLGTEMPLATE` and its four `DLGITEMTEMPLATE` records. Disassemble
`0x00465E71` to `0x0046638D` and `0x0040F63D` to `0x0040F72D`, list every call
through `0x004AE894` and `0x00465E71` with the values pushed before it, and
search the code section for `push 0xC5`.
