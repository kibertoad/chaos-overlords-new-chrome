---
id: SRC-WIN32-EDIT
title: Microsoft About Edit Controls reference
superseded_by: []
author: Microsoft
date: "2018-05-31"
location: https://learn.microsoft.com/en-us/windows/win32/controls/about-edit-controls
xxh3: null
licence: null
---

## Use

Describes the standard Windows edit control the player name dialog holds
(FND-UI-068). It says a focused edit control shows a blinking caret at the
insertion point, and that the user can type, move the insertion point and
select text with the keyboard or the mouse. `ES_UPPERCASE` turns lower-case
characters into capitals as they are entered, `ES_AUTOHSCROLL` scrolls the text
horizontally as the user types instead of accepting only what fills the box,
and `ES_NOHIDESEL` keeps the selection highlighted without the focus. Its
table of default message processing says `WM_CHAR` writes a character and
sends `EN_UPDATE` and `EN_CHANGE` to the parent, `WM_LBUTTONDOWN` moves the
insertion point and with Shift extends the selection to it, `WM_MOUSEMOVE`
with the button down changes the selection, `WM_LBUTTONDBLCLK` selects the word
under the pointer, `WM_CHAR` handles the clipboard keys such as Ctrl+C and
Ctrl+V, and Alt+Backspace undoes the last action. `WM_KEYDOWN` performs the
standard processing of the virtual keys, which the page does not list.

## Known errors

The page documents the current Windows edit control, not the one of the
Windows version the game was released for, and it describes no run of the
game. It does not say what each navigation key does.
