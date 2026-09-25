---
id: BUG-OBJECTIVE-001
title: An active player with a score below -32000 is ranked below the eliminated players
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unclear
player_reliance: unknown
evidence: [FND-AI-005]
conflicting: []
split_with: []
related: [RULE-OBJECTIVE-002]
---

## Symptom

A player still in the match can get a worse standing than the number of
active players allows: the eliminated players count as scoring higher. The
Player Rankings panel then draws that player lower than expected, and the
endgame screen lists the player after better-placed active players even when
no active player scores between them.

## Trigger conditions

A scenario whose score can fall below -32000 (Greed, which scores cash, with a
debt of more than $32,000), at least one eliminated player, and the standings
being counted at the end of a turn.

## Mechanism

While the standings are counted, each eliminated player's score is set to
-32000, and an active player's standing is the number of players with a
strictly greater score, eliminated ones included. An active score below
-32000 is therefore beaten by every eliminated player.

## Frequency

Only when an active player's score is below -32000 and someone has been
eliminated.

## Player reliance

None known.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether a timed scenario's result, or the victory splash, can go to the wrong
  player because of it is not recorded.
- Whether cash can in practice fall below -32000 in the original is not
  recorded.
