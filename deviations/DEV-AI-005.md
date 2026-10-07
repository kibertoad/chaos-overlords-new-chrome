# DEV-AI-005

- Departs from: RULE-AI-006
- Reason: A routing step of the computer players' sector selector that would leave the city is not
  taken, and the gang stays in the row or column it is in. The original takes the step whenever
  the gang count it reads for the new sector number is 5 or less, and that number then lies
  outside the player's row of `sector_gang_count`: in another player's row, in the constants
  before the list or in the selector's own pairs after it (FND-AI-066).
- Setting: None
- Default: mandatory
- Justification: Only a target read from past the end of the selector's list can lie off the
  board, and the step then stores a sector number outside the city as the gang's destination. The
  Move phase sets the gang's sector to that number and the capacity repair counts it in a 64-entry
  list (RULE-MOVE-001, RULE-MOVE-002), so the original writes outside that list and leaves a gang
  on no sector: corrupted state that no player can rely on. No recorded run reaches such a step,
  and a setting would choose between refusing the step and reproducing the corruption.
- Dropped: no
