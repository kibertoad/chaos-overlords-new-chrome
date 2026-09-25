---
id: RULE-HELP-001
title: Help Topics does nothing, and no key opens the help file
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-HELP-005, FND-UI-020, FND-UI-021, FND-HELP-003, FND-HELP-004]
conflicting: []
split_with: []
related: [RULE-UI-014, SCR-UI-009, FMT-HELP-001]
---

## Summary

The Help menu's Help Topics item is enabled but has no effect. The program
contains a call that would open the contents of the help file in the Windows
help viewer, and nothing reaches it. No accelerator key, F1 included, sends the
Help Topics command.

## When it runs

When the player chooses Help Topics from the menu bar, on any screen.

## Parameters

None.

## Inputs

`input_event`.

## Procedure

```text
if input_event.type == 1 and input_event.a == 0x80 and input_event.b == 1:
    # no loop and not the event step acts on this command; the event is handed
    # back to the loop that asked for it and ignored
    return
```

## Outputs

None. The help viewer is not started and the help file (FMT-HELP-001) is not
opened.

## Edge cases

None known.

## What the sources say

None of the sources says what Help Topics does. The shipped help file and
contents file (FND-HELP-003, FND-HELP-004) are complete and can be opened in
the Windows help viewer by hand.

## Differences between builds

None known.

## Open questions

- That choosing the item has no visible effect has not been observed in a run
  of the original.
