---
id: SRC-HELPDECO
title: helpdeco, the WinHelp decompiler, src/helpdeco.c
superseded_by: []
author: Manfred Winterhoff, Ben Collver and Paul Chapman
date: "2026-06-24"
location: https://github.com/pmachapman/helpdeco/blob/74cabd1c24de40a3f3927fc1cb3c973426b600b6/src/helpdeco.c
xxh3: null
licence: GNU GPL 2; only its behaviour is described here, no code is copied.
---

## Use

A decompiler that turns a WinHelp file back into the RTF the help compiler was
given. For a file with old 11-byte font descriptors it sets `scaling` to 10 and
`rounderr` to 5, and writes each display record's space before, space after,
line spacing and indents as `\sb`, `\sa`, `\sl`, `\li`, `\ri` and `\fi` of
`value * scaling - rounderr` twips. This agrees with SRC-WINHLP32-WINE, which
was written separately: the stored values are half-points, rounded up from the
twips the author gave.

## Known errors

None known. It recovers the author's RTF and says nothing about how the help
program draws it.
