---
id: RULE-OPTIONS-002
title: Saving the options to the registry, which always fails
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-OPTIONS-001]
conflicting: []
split_with: []
related: []
---

## Summary

The game tries to save its twelve options to the registry, but opens the key
for reading only, so nothing is saved.

## When it runs

From three places in the application flow, at `0x00460EF9`, `0x00460F40` and
`0x0046224A` in BLD-GOG-EN-1.1. What the player does to reach them is not
recorded.

## Parameters

None.

## Inputs

The first twelve options of RULE-OPTIONS-001, from `pref_thousands_colors` to
`pref_full_screen`.

## Procedure

```text
# the key is opened with KEY_READ; each of the twelve RegSetValueExA calls
# fails for want of KEY_SET_VALUE, and every result is ignored
emit PreferencesWriteFailed()
```

## Outputs

No return value. Changes nothing, in memory or in the registry. Emits
`PreferencesWriteFailed`.

## Edge cases

None known.

## What the sources say

SRC-MANUAL-GOG, page 9, describes the Options menu as the place to set the
game's preferences, which suggests they were meant to be kept.

## Differences between builds

None known.

## Open questions

- Which player actions reach the three callers (exit, a menu command, the end of
  setup).
