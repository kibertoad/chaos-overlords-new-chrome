# DEV-AI-002

- Departs from: RULE-AI-002, RULE-AI-019, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-023, RULE-AI-024, RULE-AI-025, RULE-AI-026, RULE-AI-027, RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031, RULE-MOVE-002, RULE-EQUIP-001, RULE-EVENT-014, RULE-INFLUENCE-001, RULE-SITE-001
- Reason: A computer player's planned action becomes a command only when a human could give the
  same order and the planner's running total of this turn's costs leaves cash for it; any other
  planned action is kept in the planning state and gives the gang no command. The original stores
  every planned action in the gang record and resolves it: a Move to the gang's own sector or
  into a full sector goes to the six-gang repair (RULE-MOVE-002), an Equip the player can no
  longer pay for is refused when it resolves (RULE-EQUIP-001) and leaves the player a cash report
  (RULE-EVENT-014), and an Influence in a sector the player does not control rolls with no owner
  test (RULE-INFLUENCE-001, EXP-TURN-083), and a site it completes in a neutral sector counts in
  the sector record (RULE-SITE-001). In 21 computer-only
  matches of 26 turns those three kinds came to 478 orders, and no other planned action a human
  could not order was seen. A fourth kind follows from `local_tech_cap` reading the research
  level of a sector whoever owns it (RULE-AI-026): a Research above the Tech limit the Research
  list allows there, which the original resolves because RULE-RESEARCH-001 tests no Tech Level.
  The gang's planning history is the same; its resolved action can differ.
- Setting: None
- Default: mandatory
- Justification: A computer player's gang is held to the same legal orders as a human's, so it
  cannot carry out an action no player could order. When the dropped action is a Move to the
  gang's own sector the gang stays where it is either way, and in every case its planning history,
  which later turns read, is kept.
- Dropped: no

The resolved action can differ from the original's, which changes the match when it does. Decided
2026-09-17.
