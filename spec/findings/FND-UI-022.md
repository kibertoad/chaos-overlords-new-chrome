---
id: FND-UI-022
title: One dialog procedure serves every Windows dialog; a local game can reach seven of them, and six dialog resources are never opened
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465CEC..0x00465DD4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465DD5..0x00465E9A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465EC6..0x0046638D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040F63D..0x0040F72D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449C8E..0x00449CAD
tool: Ghidra 12.1.3 and a resource listing of the executable
environment: null
---

## Observation

- `fn_00465CEC(id)` opens dialog `id` with `DialogBoxParamA`, owned by the
  window, with procedure `fn_00465EC6`. For the IDs 200, 141, 20000 and 20002
  it passes the ID as the initialization value, for all others 0. After the
  dialog closes it calls `ValidateRect` on the whole window and posts
  `WM_PAINT`, and returns the dialog's result. Every one of its 29 call sites
  pushes a literal ID. Most are preceded by `fn_00449C8E`, which calls
  `Beep(0x5312, 250)`.
- `fn_00465DD5(id)` closes any open modeless dialog through `fn_00465E1E`
  (`DestroyWindow`, then the same validate and repaint) and opens dialog `id`
  modeless with `CreateDialogParamA`, keeping its handle at `0x00487B1C`.
- `fn_00465EC6` handles `WM_INITDIALOG` by focusing the dialog and, by the
  initialization value: for 141 it loads the name of the player whose index is
  at `0x00498344` (string `13 + index`) into the text of control 1015 after its
  first 22 characters; for 20000 it puts the text at `0x004931F0` into control
  1007 and selects it; for 20002 it fills list box 1003 with the nonempty
  entries 1 to 9 of the table at `0x00493270` (32 bytes each) and selects the
  first; for 200 it resizes the dialog. On `WM_COMMAND` a control ID below 8
  ends the dialog with that ID as result (OK 1, Cancel 2, the third button 3);
  control 1012, or a double click in list box 1003, ends it with the list
  selection plus 10. It copies the text of control 1007 to `0x00498470` and of
  control 1008 to `0x00498370` (254 characters each) when it handles other
  commands.
- `DIALDIALOG` and `DIRECTDIALOG` are opened by name from the modem and serial
  code, `fn_0041F7BD` and `fn_00424E55`, with their own procedures.

The dialogs opened, by the literal each call pushes (roles in our words; the
texts are not reproduced):

| Dialog | Kind | Opened by | Role |
|---|---|---|---|
| 129 | modal, buttons 1, 2, 3 | main console `fn_0046FD80` on End, Exit, and in its CD loop | The game is not saved: save first (1), cancel (2) or go on without saving (3) |
| 130 | modeless | `fn_00422729` (3 sites) | Searching for a host, Escape cancels |
| 131 | modal | `fn_00462579`, `fn_0040CED0` | Disconnected from the host |
| 132 | modal | `WinMain` | The surfaces could not be created |
| 135 | modal | `WinMain` | No image set can be used |
| 136 | modal | `fn_00462579` | Shown before Thousands of Colors or Full Screen is flipped |
| 137 | modal | image loader `fn_00464108` when `fn_00464155` fails | A file failed to load; the game goes on |
| 138 | modal | `fn_0046D00D` | The host confirms disconnecting every player |
| 139 | modal, edit 1007 | `fn_0040F63D`, from setup `fn_0040E0A0` and the network setups | The player's name |
| 140 | modal | `fn_0046D00D` | A client confirms disconnecting from the host |
| 141 | modal | `fn_004650B1` | The connection to a named player was lost |
| 143 | modal | `fn_0046BA84` | The game joined does not recognise this version |
| 144 | modeless | `fn_00458155` | Fetching data from the players |
| 20000 | modal, edit 1007 | `fn_0042364F` | The host's address to join |
| 20002 | modal, list 1003 | `fn_00424AE5` | The local address to host on |
| 20004 | modal | `fn_00422729` (4 sites) | The host does not answer |
| 20005 | modal | `fn_004215BB` (2 sites) | The network ring buffer could not be allocated |
| 20006 | modeless | `fn_00421B9E` | Waiting for a connection, Escape cancels |
| 20007 | modal | `WinMain` (2 sites) | The display depth and the installed image set do not match |

No call opens dialogs 128, 133, 134, 145, 201 or 20003; the special case for 200
in both functions names a dialog the resource section does not hold.

`fn_0040F63D(player)` clears `0x00498470`, opens dialog 139 and sets the arrow
pointer. When the result is 1 and the text is not empty, it copies up to 10
characters into the name at `0x004A2588 + player * 12`, replacing every
character outside the range space to `Z` (32 to 90) with a space, and stores the
length in the name's first byte.

## Interpretation

The Windows dialogs are alerts, confirmations and two text entries; the game
screens themselves are drawn by the game. In a local game the player can meet
dialogs 129, 132, 135, 136, 137, 139 and 20007. A player name keeps capitals,
digits and punctuation up to `Z`; lower-case letters become spaces. Dialog 128
(an idle gangs warning), 133 (an offer to change the display colours), 134, 145,
201 (a demo notice) and 20003 (a request to insert the CD) are left over: the
idle gangs warning is drawn by the game (RULE-OPTIONS-003) and the CD check
never asks for the disc through a dialog.

## Alternatives

A dialog ID computed at run time would not show as a literal push; all 29 calls
to `fn_00465CEC` and all 5 to `fn_00465DD5` were checked and push literals.

## How to reproduce

List the callers of `0x00465CEC` and `0x00465DD5` and read the value each call
site pushes last. The `DialogBoxParamA` calls through slot `0x004AE810` are at
`0x00465D24`, `0x00465D4F`, `0x00424E81` and `0x0041F7DE`.
