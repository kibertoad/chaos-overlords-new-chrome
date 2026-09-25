---
id: FND-AI-020
title: The family-1 handler chooses Heal, Chaos, Snitch, Control or Move from the previous action, cash and Mentality
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00434080
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004345D0..0x00434712
tool: Ghidra 12.1.3
environment: null
---

## Observation

Handler `0x00434080` switches on the gang's previous action (planning record
+5). The six calls with selector `0x36` (Mentality) at `0x0043466A`,
`0x004346D3`, `0x00434DA6`, `0x00434DD9`, `0x00435090` and `0x004350C3` form
three pairs of gates. The recorded branches are:

- Previous action 0 (None) or 3 (Chaos): when Force is below 8 and effective
  Heal is at least -3, it writes Heal (7) if selector `0x2A` reports no
  Crackdown in the current sector, and Move (10) through sector selector mode 5
  if one is in force. Otherwise, when the older action (+2) is 13 (Snitch) it
  writes Chaos (3), and for any other older action Move through mode 5.
- Previous action 7 (Heal): when Force is below 9 and effective Heal is at least
  -3 it writes Heal again. Otherwise it writes Control (4) when selector `0x2C`
  accepts the current sector, and Move through mode 5 when it does not.
- Previous action 4 (Control), 5 (Equip) or 13 (Snitch): it first tries the
  equipment choice of FND-AI-021. When that writes no Equip, the continuation at
  `0x004345D0..0x00434712` runs. Selector `0x35` tests whether the current
  sector's owner is human. A human owner enters the crime branch when cash is at
  least 50 and Mentality is at least 1. A non-human owner enters it only when
  the raw owner differs from the active player, is strictly greater than 0,
  cash is at least 50, and Mentality is exactly 0. The crime branch writes Chaos
  when the sector's Tolerance is at most 3 and Snitch from 4 up. Every failed
  gate writes Move through mode 5.
- Another path keeps Snitch only when cash is strictly greater than 50, and
  otherwise writes Move.

All four Heal writes first require effective Heal at least -3. Three require
Force below 9, and the previous-None-or-Chaos path requires Force below 8. No
recovered family-1 path heals at Force 9. The exact-Mentality-2 gate of the
third pair has not been tied to a branch.

## Interpretation

Family 1 is a crime-and-healing family: it heals injured gangs, raises Chaos in
low-Tolerance sectors, snitches elsewhere, and wanders through mode 5 when
nothing applies. At Criminal and above it commits crimes in human-owned
sectors; at Goon it does so only in computer-owned sectors, and never in
player 0's.

## Alternatives

The "owner strictly greater than 0" test excludes player 0 as an owner even
when player 0 is a computer; it reads as a signed comparison with 0 where a
comparison with -1 (neutral) may have been meant, but the finding records the
comparison as it is. Which previous actions lead to the "keep Snitch while
cash is above 50" path, and the branch that uses the exact-Mentality-2 gate,
are not recorded. What the handler does for previous actions 1, 2, 6, 8, 9,
10, 11, 12 and 14 is not recorded.

## How to reproduce

Open `0x00434080` and find the switch on selector `0x3E`. The Mentality
pushes of `0x36` are at the addresses listed. The post-equipment continuation
occupies `0x004345D0..0x00434712`; its cash comparison is with 50 (`0x32`) and
its Tolerance comparison with 3.
