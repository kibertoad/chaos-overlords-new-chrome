---
id: BUG-AUDIO-001
title: The turn-start sound plays even with sound effects turned off
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: presentation
intent: unclear
player_reliance: unknown
evidence: [FND-AUDIO-003, FND-AUDIO-002, FND-AUDIO-006, FND-AUDIO-007, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AUDIO-006, RULE-AUDIO-005]
---

## Symptom

With the Sound Effects level at 0, every other sound effect is silent, but the
turn-start sound still plays at each new turn.

## Trigger conditions

Sound effects set to level 0, in a network game of the original's protocol,
on the hosting computer or a computer that joined, at any turn start after the
first. A game started with New Game never plays the sound (RULE-AUDIO-006).

## Mechanism

Every other effect is played through a wrapper that tests the effects-enabled
byte. The turn-start sound is played by a direct call to the lower helper,
which does not test it.

## Frequency

Every turn start after the first.

## Player reliance

None known.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether the auxiliary volume the game sets for effects reaches `PlaySoundA`
  output at all: at level 0 it is set to 0, but on the device the setup finds by
  a technology value no Windows device reports, which is usually device 0
  (FND-AUDIO-007).
- Whether the direct call was meant to make the cue play regardless of the
  setting.
