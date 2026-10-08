# DEV-UI-007

- Departs from: SCR-UI-003, SCR-UI-004, RULE-UI-011
- Reason: Hovering the Tolerance value shows the range the player's queued Chaos can reach, and
  the value turns orange when that range can set off a Crackdown. On a seat's view of an online
  match (`docs/MULTIPLAYER.md`, "Planning on a view") the tooltip gives a sector's base and its
  sites' part only in the seat's own sectors.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. A Crackdown follows from
  Tolerance and the Chaos the player queued, both known to the player, but the original leaves the
  player to work out the range by hand, and a miscalculation sets off a Crackdown the player did not
  intend. The warning only states that result in advance, removes no control and changes no rule;
  the player can still order the Chaos.
- Dropped: no
