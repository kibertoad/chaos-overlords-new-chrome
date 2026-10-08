---
id: SRC-WINHLP32-WINE
title: Wine's WinHelp viewer, programs/winhlp32/hlpfile.c
superseded_by: []
author: The Wine project
date: "2023-02-07"
location: https://github.com/wine-mirror/wine/blob/a6f3e4ad222cd78eee20639c2c7b8f7315c13070/programs/winhlp32/hlpfile.c
xxh3: null
licence: GNU LGPL 2.1 or later; only its behaviour is described here, no code is copied.
---

## Use

A reimplementation of the Windows help program that reads WinHelp 3.1 files and
turns each display record into RTF for a rich edit control. It is read for
three things the help file's records leave implicit:

- In `HLPFILE_BrowseParagraph`, the space before, space after, line spacing,
  left, right and first-line indents of a display record are compressed signed
  values that become the RTF `\sb`, `\sa`, `\sl`, `\li`, `\ri` and `\fi`
  amounts in twips as `value * scale - rounderr`. For a file whose `|FONT`
  uses the old 11-byte descriptors, `HLPFILE_ReadFont` sets `scale` to 10 and
  `rounderr` to 5, so a stored value is a count of half-points, and the twips
  are the value times 10 less 5.
- Command `0x81` inside a display record becomes `\line`, a line break inside
  the paragraph, and command `0x82` becomes `\par`, the end of a paragraph.
  The text after a `0x82` starts a new paragraph with the record's formatting.
- A font descriptor whose face index is past the face table is given the face
  `Helv` and a diagnostic, and the file is read on.

## Known errors

It is not the Windows help program. Where its rendering differs from the
native viewer's, nothing here shows which one is right, and its rich edit
layout is not evidence of the native layout.
