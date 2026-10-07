# POLICE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-POLICE-001` | In a Crackdown sector the police may find each gang and attack it with 25 minus its Defense in dice | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | EXP-TURN-010's first run has two police detection draws in turn 23 and 24, each followed by the police dice. |
| `RULE-POLICE-002` | A Crackdown is recorded in the sector's history, and a third within five turns neutralizes the sector and adds 3 to 5 turns of police | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | EXP-TURN-010's first run neutralizes a sector with its third Crackdown and draws the police turns. |
| `RULE-POLICE-003` | Police presence counts down by one at the end of every turn unless it is permanent | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | EXP-TURN-010 compares every sector's police turns after the countdown. |
| `RULE-POLICE-004` | Crackdown reports go to the players who had a gang in the sector when resolution began | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | The replays compare every Crackdown report; EXP-TURN-023 and EXP-TURN-024 report to players that raised no Chaos in the sector, and EXP-TURN-024 to one with no gang left there at the end of the turn. Supplemental observations and coverage limits: EXP-TURN-036. |
