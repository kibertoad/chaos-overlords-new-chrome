---
id: SRC-WIN32-EDIT-TEXT
title: Microsoft Edit Control Text Operations reference
superseded_by: []
author: Microsoft
date: "2018-05-31"
location: https://learn.microsoft.com/en-us/windows/win32/controls/edit-controls-text-operations
xxh3: null
licence: null
---

## Use

Says that when the user selects an edit control, by clicking it or moving to it
with Tab, the system gives it the keyboard focus and highlights its text, and
that the default limit on the text a user can enter is 32 KB until an
application sends `EM_SETLIMITTEXT`. It supports the start state of the
player name dialog's control and its text limit (FND-UI-068).

## Known errors

The page documents the current Windows edit control and gives the limit only
as 32 KB; SRC-WIN32-EM-LIMITTEXT gives it in characters.
