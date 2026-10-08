# DEV-MOVE-002

- Departs from: RULE-MOVE-002
- Reason: The rebuild's Move repair counts the times it gives a mover sent back to its own
  sector a random neighbour. After 256 of them in one player's repair, it sends that mover to the
  lowest-numbered sector the player's projected count leaves room in, which need not be a
  neighbour. The original's loop has no bound, and some order sets keep it running for ever
  (FND-MOVE-006).
- Setting: None
- Default: mandatory
- Justification: In the original those orders hang the game in the Move phase, which the Fidelity
  rules allow to be fixed. Below the bound the rebuild follows the original's loop, and a setting
  that brings back the hang gives the player nothing.
- Dropped: no
