---
id: BUG-OPTIONS-002
title: An option missing from the registry takes the value of the option read before it
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: presentation
intent: unintended
player_reliance: not-relied-on
evidence: [FND-OPTIONS-001, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-OPTIONS-001]
---

## Symptom

When the registry key holds some options but not others, a missing option
starts with the value of another option instead of its default: for example a
missing `prefsVolumeCD` gives the music the effects level.

## Trigger conditions

A registry key that exists but lacks one or more of the thirteen values, or
holds one that cannot be read into four bytes.

## Mechanism

The loader reads all thirteen values into one four-byte buffer, never resets it
between queries, ignores each query's result, and copies the buffer into the
option's global after each query. A failed query leaves the previous value in
the buffer.

## Frequency

Every start while the key is in that state.

## Player reliance

None known.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- What the options get when the key does not exist at all (RULE-OPTIONS-001).
