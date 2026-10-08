---
id: SRC-WIN32-DIALOG
title: Microsoft Dialog Box Programming Considerations reference
superseded_by: []
author: Microsoft
date: "2020-08-25"
location: https://learn.microsoft.com/en-us/windows/win32/dlgbox/dlgbox-programming-considerations
xxh3: null
licence: null
---

## Use

Describes the dialog manager that runs the player name dialog (FND-UI-068).
When the dialog itself receives `WM_SETFOCUS` with no saved control, the
default processing gives the focus to the first control in the template that is
visible, enabled and has `WS_TABSTOP`; an edit control that receives the focus
selects all of its text. The keyboard interface of a modal dialog sends
`WM_COMMAND` with `IDOK`, or the default push button's ID, for Enter and with
`IDCANCEL` for Escape, and moves the focus between `WS_TABSTOP` controls with
Tab and Shift+Tab. Arrow keys go to the control with the focus when it asks
for them through `WM_GETDLGCODE`, which an edit control does
(SRC-WIN32-EDIT).

## Known errors

The page documents current Windows and describes no run of the game.
