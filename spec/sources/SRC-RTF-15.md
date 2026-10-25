---
id: SRC-RTF-15
title: Microsoft Rich Text Format specification, version 1.5
superseded_by: []
author: Microsoft
date: "1997"
location: http://www.biblioscape.com/rtf15_spec.htm
xxh3: null
licence: null
---

## Use

The help compiler builds a help file from RTF, and SRC-WINHLP32-WINE and
SRC-HELPDECO map a display record's fields back to RTF paragraph control words.
This specification defines those words: `\sb` is the space before a paragraph,
`\sa` the space after it, `\li`, `\ri` and `\fi` the left, right and first-line
indents, all in twips. `\sl` is the space between lines: when it is missing the
tallest character of the line sets the spacing, a positive value is used only
when it is taller than the tallest character, and a negative value is used as
its absolute value even when that is shorter.

## Known errors

It describes RTF as a word processor reads it. It does not say how the
Windows help program combines the space after one paragraph with the space
before the next, and neither does any other source found.
