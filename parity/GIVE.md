# GIVE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-GIVE-001` | Give empties the giver's selected slots and holds the items for delivery to the recipient after the player's scan | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | EXP-TURN-027 replays a human's Give of a weapon and an armor to a gang hired the turn before, and EXP-TURN-030 a swap by two Gives, then two Gives to one gang that buys a weapon in the same turn. |
| `SCR-GIVE-001` | Give panel | supported | complete | tests/Rechaos.Tests/ScreenCaptureTests.cs | `DEV-GIVE-001` | validated | The recipient cards, eligibility, marks, faces and keys follow the original. The list has no background fill, and ineligible cards are blacked out through bitmap 146 from the card's corner (FND-GIVE-003). ScreenCaptureTests compares the panel with one recipient and no item selected in the EXP-UI-010 capture with the original, drawing the recorded item frame (FND-UI-053), and no element differs; no capture shows a selection, a chosen recipient or a dimmed card. |
