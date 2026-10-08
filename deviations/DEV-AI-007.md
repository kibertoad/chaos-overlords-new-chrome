# DEV-AI-007

- Departs from: RULE-AI-006, RULE-AI-031, RULE-MOVE-001
- Reason: A computer player's gang moves only to a sector next to its own, as a human's does. The
  original's sector selector can return a sector several steps away: when the first of its sorted
  pairs is a neighbour it returns the tie-break's pick, and a pair that the family-0 and family-1
  filter kept from an earlier call can tie with the neighbours (RULE-AI-006). A family-13 or
  family-14 gang outside Big Man and Siege moves to its planned target, sector 0, from anywhere in
  the city (RULE-AI-031, EXP-TURN-101). The Move pass then
  puts the gang in that sector at once (RULE-MOVE-001, EXP-TURN-015). The rebuild refuses such a
  Move when the planned action becomes a command, as DEV-AI-002 does with the other planned
  actions a human could not order, so the gang has no order that turn and keeps its planning
  history.
- Setting: `--original-computer-moves` on the game's command line, inverted (the flag switches the
  deviation off for the local matches started in that session). No screen offers it, and an
  online match keeps the deviation on.
- Default: on
- Justification: The jump comes from a pair left over from another gang's search, which the
  selector was not written to return, and it lets a computer player's gang do what no human's can:
  the same rules apply to every player. It costs no measurable balance. Such Moves come to about
  25 a match in Kill 'Em All and 12 in Power and Big 40. In 973 pairs of matches of every scenario,
  played to turn 208 from the same seeds with a planner-played human seat that could not jump, the
  human seat survived to the end in 45% of the matches when the computer players could jump and in
  47% when they could not, a difference within the matches' noise (95% interval of 2.8 points
  either way), and its turns survived and sectors held did not change beyond noise either. Since
  no player can notice the difference, the setting stays off the Options screen; the flag serves
  the replays of recorded runs, which run with it switched off, and a player who wants the
  original's Moves.
- Dropped: no
