---
id: RULE-SETUP-008
title: A local human's planning opens with the Ready card when several humans share the computer, then Game Information, combat results and Last Turn Events
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-010, FND-AUDIO-002, FND-AUDIO-012, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [SCR-SETUP-002, RULE-COMBAT-004, RULE-EVENT-005]
---

## Summary

When more than one human plays on the same computer, each human's turn starts
behind a card that asks that player to press Ready, so the previous player
does not see it. Then, in this order, the game shows Game Information (once,
in a new game), the results of the last turn's combat, the Last Turn Events,
and sounds the Comlink alert if the player has unread messages, before the
city opens for orders.

## When it runs

In `planning_phase`, for each active player slot whose controller is 0 (a
local human), when the outer match loop enters that player's planning.

## Parameters

- `player`, the player slot whose planning starts.
- `handoff`, true when more than one local human was active when the
  planning scan began.

## Inputs

`new_local_game`, `pref_detailed_combat`, the player's `comlink_messages`.

## Procedure

```text
if handoff:
    # blocks until Ready is released inside its button
    show SCR-SETUP-002
active_player = player
if new_local_game:
    # the Game Information panel, once in a new local game
    fn_0045519D()
if pref_detailed_combat:
    call RULE-COMBAT-004(player)
else:
    # the Combat Results panel; returns at once when no sector qualifies
    fn_00451F80()
# the Last Turn Events read-state table is rebuilt here
call RULE-EVENT-005()
comlink_pending = 0
for i in 0..16:
    let m = comlink_messages[player * 16 + i]
    if m.occupied and not m.read:
        comlink_pending = 1
if comlink_pending:
    emit ComlinkAlert()
comlink_alert_repeat = 0
```

## Outputs

No return value. Sets `active_player`, `comlink_pending` and
`comlink_alert_repeat`, and shows, one after another, the screens named above.
Each of them blocks until the player closes it. Emits `ComlinkAlert` when the
player has an unread message. Makes no draws.

## Edge cases

With one local human, `handoff` is false and the Ready card never appears,
even when computer players or eliminated local players plan between that
human's turns. With nothing to report, the Combat Results panel returns at
once and the events panel follows directly.

## What the sources say

SRC-MANUAL-GOG, page 15, says that players on the same computer take turns
giving orders and pressing Done, and that the next player's face then appears.
It does not give the order of the panels that follow.

## Differences between builds

None known.

## Open questions

- `fn_0045519D` is the Game Information panel's presenter and `fn_00451F80`
  the Combat Results presenter; their screens are described with the
  interface and combat panels, and these neutral names stand until those
  entries exist.
- Where `new_local_game` is cleared, and its address, are not recorded.
- Whether the Ready card is shown before the first local human of every turn,
  or only between two local humans, is not recorded; the finding describes the
  gate as "more than one active local human".
- The address of the option `pref_detailed_combat` is not recorded here.
