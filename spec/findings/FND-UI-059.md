---
id: FND-UI-059
title: Only the planning entry draws the console's calendar, score and cash, before any presentation
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FD80
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ghidra's references to the per-player score array `0x004A2790`, the cash
array `0x004A25E8` and the elapsed-turn count `0x0049CA68` lie in these
functions only:

- the planning entry `fn_0046FD80`, which reads the elapsed turns at
  `0x0046FF2A`, `0x0046FF73` and `0x0046FFD6`, the score at `0x0047006C` and
  the cash at `0x004700AF`, for the console draws of FND-UI-040;
- the awards screen `fn_0042CE61` (score, `0x0042D564`);
- the new game and turn loop `fn_0046E766`, the resolver `fn_00472775` and
  the scoring function `fn_0047712A`, which write them;
- the load `fn_0046381A` and the save `fn_00463CC5`, which pass each array's
  address to a block copy;
- the network functions `fn_0046981D`, `fn_0046A115`, `fn_0046A7CB` and
  `fn_0046BA84`;
- the AI and objective tests `fn_00402D70`, `fn_004078D9`, `fn_004518D9`,
  `fn_00458FA0` and `fn_00476857`;
- the Events compositor `fn_0044FD6C` and Comlink `fn_0045EAB1`, which read
  the elapsed turns only (`0x004508F1`, `0x00450923`, `0x00450970`,
  `0x0045F119`).

In `fn_0046FD80` all five reads come before the combat presentation at
`0x00470278..0x0047029D`, which opens Detailed Combat or Combat Results
(FND-COMBAT-010), and before Last Turn Events at `0x004702A5..0x00470356`
(FND-EVENT-005).

## Interpretation

The console's calendar, score and cash rows are drawn only at a planning
entry, from the values the resolution and scoring left. They are drawn before
the planning entry shows the last turn's combat and events, so those
presentations open over the new values. No other function draws the rows,
so a value written after the entry shows only at the next planning entry.

## Alternatives

A function could reach the arrays through a pointer that no reference
records. The four functions that push an array's address pass it to a block
copy for saving, loading or a network message, and none of those copies
draws. When the writers run relative to a human's planning, for example a
load chosen from the menu while planning, and whether a planning entry
follows it, is not part of this reading.

## How to reproduce

Verify the executable against BLD-GOG-EN-1.1. In Ghidra, list the references
to `0x004A2790`, `0x004A25E8` and `0x0049CA68` (`tools/ghidra/ReportReferences.java`)
and group them by containing function. In `fn_0046FD80`, compare the
addresses of the reads with the calls at `0x00470278..0x0047029D` and
`0x004702A5..0x00470356`.
