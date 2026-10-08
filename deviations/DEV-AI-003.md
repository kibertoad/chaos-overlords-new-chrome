# DEV-AI-003

- Departs from: RULE-AI-001, RULE-AI-002, RULE-AI-019, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-023, RULE-AI-024, RULE-AI-025, RULE-AI-026, RULE-AI-027, RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031, RULE-UI-009, SCR-UI-008
- Reason: A second computer-player policy keeps every command the original planner chooses and
  gives an idle gang at most one legal, affordable fallback of its own scoring, examining gangs in
  ascending order. Resolution odds are unchanged. With Advanced AI on, Game Information names the
  policy after the Mentality text.
- Setting: Advanced AI (Original is the original planner)
- Default: off
- Dropped: no

The policy is chosen for a new match and kept by it; loaded saves keep the policy they were
started with.
