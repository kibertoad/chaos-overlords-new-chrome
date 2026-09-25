---
id: FND-HIRE-003
title: The Hire comparison panel draws each value two cells wide and signed, and never compares the three offers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004546C5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414187
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004142E7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00454A85
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00454ACE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00454B17
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Hire comparison renderer `0x004546C5` draws, for each offer, six values
with the number helper `0x00414187`: Tech Level, Upkeep, Combat, Defense,
Stealth and Detect. It then draws ten modifier rows with the number helper
`0x004142E7`. Every call passes the literal width 2 and a signed 16-bit value.
The three consecutive modifier calls at `0x00454A85`, `0x00454ACE` and
`0x00454B17` each read the next signed 16-bit field before passing that
width. After each call the renderer goes straight to computing the next
value's destination. It contains no comparison between the three offers and
no choice of a separate colour for a best value.

## Interpretation

Each value on the panel is a fixed two-cell field drawn by the game's common
number helpers: a one-digit value takes the right cell, and a negative value
is drawn with the red digit row, as its absolute digits, without a minus sign.
The helpers draw a zero in bright green for a baseline value and dim green for
a modifier (FND-UI-006). The panel gives no hint of which offer is best.

## Alternatives

A colour tint marking the best of three values was considered and is ruled
out by the absence of any comparison in the renderer.

## How to reproduce

Find the callers of `0x00414187` and `0x004142E7`; `0x004546C5` is the one
that loads the Hire comparison panel. The calls at `0x00454A85`, `0x00454ACE`
and `0x00454B17` show the literal width 2.
