# DEV-AI-008

- Departs from: RULE-AI-012, RULE-HIRE-001
- Reason: A computer player's hire goes only to a sector the player controls or holds a gang in, as
  a human's does. The original's hire resolver has no such test, so a hire the computer planner
  places anywhere else is carried out: the new gang's Force is rolled and the gang appears there
  (RULE-HIRE-001, EXP-TURN-090). The rebuild drops such a hire when the planner makes it, so the
  player hires nothing that turn and the offer stays in its pool.
- Setting: `--original-computer-hires` on the game's command line, inverted (the flag switches the
  deviation off for the local matches started in that session). No screen offers it, and an
  online match keeps the deviation on.
- Default: on
- Justification: The same rules apply to every player, and the difference costs no measurable
  balance. The original tests the owner where each hire is placed instead of in the resolver:
  the hire panel accepts a drop only on a sector the player owns or holds a living gang in
  (SCR-HIRE-002), and the two random modes of the computer's own destination helper choose only
  among such sectors (RULE-AI-012). The planner's encoded sector passes neither place, so the
  hire EXP-TURN-090 reaches falls through a gap between them. No recorded or simulated match
  reaches it without the probe's written families, so no player meets it or can build a strategy
  on it. In 500 pairs of four-year matches, 50 seeds of every scenario played from the same
  seeds with a planner-played human seat that keeps the human hire rule, no computer hire went to
  such a sector, and every pair ended the same way with the deviation on and off: computer players
  won 164 of the 438 matches that ended, and the human seat survived in 173. The other 62 pairs,
  untimed matches still running at the simulation's turn limit, stopped at the same turn with the
  same events either way. EXP-TURN-090 reaches such a hire only after the probe writes family 5
  into every computer gang. Since no player can notice the difference, the setting stays off the
  Options screen; the flag serves the replays of recorded runs, which run with it switched off,
  and a player who wants the original's hires.
- Dropped: no
