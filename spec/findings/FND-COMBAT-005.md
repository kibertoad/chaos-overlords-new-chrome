---
id: FND-COMBAT-005
title: Detailed Combat plays one clip per attack, for the viewer's gangs in sector order, and shows a retaliation inside the attack's clip
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E040
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043087E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430C23
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Detailed Combat `0x0042E040` takes the viewing player and walks only that
  player's gangs, sector by sector and then by sector slot, from the per-sector
  table the resolver fills (FND-COMBAT-004).
- For each such focal gang, `0x0043087E` builds an ordered list: the focal
  gang; then the focal gang's own target; then every other gang in the sector
  whose target is the focal gang, in player order and then slot order, leaving
  out the target already listed; then one police entry, the value `0xFFFE`,
  when the focal gang's record carries police damage.
- Before the presenter plays anything, it sets every listed gang's displayed
  Force (record byte 3) to its phase-start Force (byte 1).
- The presenter makes two tests on each listed entry:
  - When the entry is the focal gang's target, it loads the strips
    `7000 + n` and `7100 + n` and the focal gang's attack sound, subtracts the
    focal record's byte 4 from the target's displayed Force and the focal
    record's byte 5 from the focal gang's displayed Force, and calls the
    timeline `0x00430C23`.
  - When the entry's target is the focal gang, it loads the strips `7200 + n`
    and `7300 + n` with the entry's own attack sound, subtracts the entry's
    byte 4 from the focal gang's displayed Force and the entry's byte 5 from
    the entry's displayed Force. The police entry takes this branch with the
    strip `7228` and the strip `7320` or `7301`, and the sound `SND00518`.
- An entry that passes both tests, a pair of gangs attacking each other, has
  its first clip called with the timeline's hold flag cleared, so the timeline
  leaves at tick 16 and the second clip follows at once. Every other clip holds
  the result through tick 21.
- The timeline compares each Force bar with its width before the damage and
  draws each changed bar in white on ticks 13 and 15.

## Interpretation

Retaliation has no clip and no sound of its own. It is taken off the
attacker's bar inside the attacker's own clip. The `72xx`/`73xx` strips mean
that the viewer's gang is attacked; they do not mean retaliation. A gang attack
is shown only through a gang the viewer owns, and clips are ordered by sector,
then by the viewer's sector slot, then by the list above. A bar never shows
damage from a fight the viewer is not shown. Two gangs that attack each other
play as two clips back to back, with no hold between them, which can look like
one fight with a strike-back.

## Alternatives

The earlier reading, that the mirrored strips show a retaliation, is ruled out
by the branch conditions above. Whether the reset of displayed Force happens
once for the whole presentation or once per focal gang's list was not
recorded.

## How to reproduce

Find the loads of the resource bases 7000, 7100, 7200 and 7300 in `0x0042E040`
(FND-UI-001 gives their instruction addresses), follow the list builder
`0x0043087E` it calls, and read the timeline `0x00430C23` for the hold flag and
the ticks 13, 15, 16 and 21.
