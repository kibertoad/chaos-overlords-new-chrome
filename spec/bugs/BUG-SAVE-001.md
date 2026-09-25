---
id: BUG-SAVE-001
title: Loading a truncated save overwrites part of the game state and keeps the rest
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: save-corruption
intent: unintended
player_reliance: unknown
evidence: [FND-SAVE-001, FND-PLATFORM-003]
conflicting: []
split_with: []
related: [FMT-SAVE-001]
---

## Symptom

Loading a save file that is shorter than 45,305 bytes but starts with a valid
marker fills the leading blocks from the file and leaves every later block
holding what the running game had before. The load then fails on the closing
marker and beeps, but the state it already overwrote stays overwritten.

## Trigger conditions

A save file that starts with `S40W` or `N40W` and ends before its closing
marker, for example after a disk-full write or a copy that stopped early.

## Mechanism

The load function reads the marker and then the 44 blocks of FMT-SAVE-001
straight into the live globals through the read wrapper, and checks the byte
count of none of the reads (FND-SAVE-001, FND-PLATFORM-003). Only the three
preference bytes are held back until the closing marker matches. A read past
the end of the file transfers fewer bytes or none, and the blocks it covers
keep their old contents.

## Frequency

Every load of a truncated file. A save written in full is not affected.

## Player reliance

None known.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- What the game shows and allows after such a failed load has not been
  observed: whether the mixed state is played on, or the caller returns to a
  screen that discards it.
