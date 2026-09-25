---
id: BUG-OPTIONS-001
title: Changes made in the Options menu are never saved
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: presentation
intent: unintended
player_reliance: not-relied-on
evidence: [FND-OPTIONS-001, FND-RNG-001, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-OPTIONS-001, RULE-OPTIONS-002]
---

## Symptom

Options the player sets, such as the music level, Slide Panels or Warn if Idle
Gangs, are back to their earlier values the next time the game starts.

## Trigger conditions

Changing any option and restarting the game.

## Mechanism

The writer opens `HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0` with
`KEY_READ` (`0x20019`) and then calls `RegSetValueExA` for each of the twelve
options. A handle without `KEY_SET_VALUE` cannot write, every call fails, and
the writer ignores the results. The loader tries to store a new serial number
through the same kind of read-only handle, which fails too, so a key without a
serial number stays without one and the loader makes its two random draws again
at every start (RULE-OPTIONS-001). Values put in the key by other means, such as
an installer, are still read.

## Frequency

Every time.

## Player reliance

None known. Nothing in the rules depends on the options, and the extra random
draws at startup happen before any game is set up from the generator.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether the original installer writes the key, and with which values.
- Whether Windows 95, which the game was written for, ignored the access mask
  and let the writes succeed; if so the defect shows only on later versions of
  Windows.
