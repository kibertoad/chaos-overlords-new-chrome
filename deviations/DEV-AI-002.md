# DEV-AI-002

- Departs from: RULE-AI-002, RULE-AI-019, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-023, RULE-AI-024, RULE-AI-025, RULE-AI-026, RULE-AI-027, RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031, RULE-MOVE-002, RULE-INFLUENCE-001, RULE-SITE-001, RULE-CONTROL-001
- Reason: A computer player's planned action becomes a command only when a human could give the
  same order; any other planned action is kept in the planning state and gives the gang no
  command. The original stores every planned action in the gang record and resolves it: a Move to
  the gang's own sector or into a full sector goes to the six-gang repair (RULE-MOVE-002), and an
  Influence in a sector the player does not control rolls with no owner test
  (RULE-INFLUENCE-001, EXP-TURN-083); a site it completes in a neutral sector counts in the
  sector record (RULE-SITE-001). Cash does not decide which planned actions become commands: a
  human may order more Equips than the player can pay for, and so may a computer player, whose
  Equips are tested against its cash when they resolve as the original's are (RULE-EQUIP-001,
  FND-AI-082). A third kind follows from `local_tech_cap` reading the
  research level of a sector whoever owns it (RULE-AI-026): a Research above the Tech limit the
  Research list allows there, which the original resolves because RULE-RESEARCH-001 tests no
  Tech Level. A fourth is a family-2 or family-13 Control (RULE-AI-021, RULE-AI-031) of a
  sector the player already controls or one under police presence. The original resolves it:
  under police presence the Control adds the gang to the pool and takes nothing
  (RULE-CONTROL-001), and in the player's own sector it counts the gang twice, as an attacker
  and as a defender, if another player tries to take the sector in the same turn. In 120 matches
  of six computer players, every scenario at every Mentality played for up to 208 turns, the
  rebuild dropped 1,360 Influences, 1,944 Researches and 14 Controls,
  and no other planned action. `AiTournamentTests` fails on any kind this entry does not list.
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
