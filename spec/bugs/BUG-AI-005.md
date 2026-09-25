---
id: BUG-AI-005
title: A computer player far behind the leader late in a match never switches its gangs to family 9, because the flag store uses the wrong index
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: not-relied-on
evidence: [FND-AI-043]
conflicting: []
split_with: []
related: [RULE-AI-001, RULE-AI-027]
---

## Symptom

A computer player that trails badly in the last turns of a match keeps
planning with its usual strategy families. The planner contains a switch to
the raider family 9 for this case, and it never takes effect.

## Trigger conditions

In the planning pass of a computer player, 13 or fewer turns remain, the
player's `scenario_score` is below one fifth of the leader's, and at least four
players stand above it in `scenario_standing`.

## Mechanism

The pass sets its per-player family-9 flag with the counter of the sector loop
that runs just before, which holds 64 at that point, instead of the player
slot. The flag of the player stays 0, and the byte written belongs to a table
of floats that only uncalled code reads. The dispatch loop that follows tests
the player's flag, finds it clear, and leaves the families alone.

## Frequency

Every planning pass that meets the trigger conditions. With six players the
standing condition needs a player in fifth or sixth place.

## Player reliance

None known. The switch has never taken effect, so no player strategy can have
been built on it.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether the designers meant the switch to last for the rest of the match,
  as the flag does when a network player is taken over (RULE-AI-001), is
  inferred from the flag never being cleared during a match.
