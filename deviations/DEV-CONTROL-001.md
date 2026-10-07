# DEV-CONTROL-001

- Departs from: RULE-CONTROL-001, BUG-CONTROL-001
- Reason: Only the players who ordered Control in the sector, and its owner, enter the
  comparison, with the original's arithmetic and tie draw among them. The original compares every
  player, so a player with no Control order there has the margin -(Income + Support) and is handed
  the sector, or drawn with the real challenger, when that sum is negative. The shipped site table
  has sites with negative Support, so the case can arise.
- Setting: None
- Default: mandatory
- Justification: The original's behaviour is a bug (BUG-CONTROL-001). The pass gives every player
  slot a pool, and a player with no order there starts at 0, so when the sector's Income plus
  Support is negative that player's margin comes out positive. The player is then handed a sector
  they never tried to take, or ties with and can beat a challenger who would otherwise win alone.
  Nothing about it reads as design: the manual describes the comparison only among players who
  try to control the sector, the case needs completed sites with negative Support and no police,
  the player gets no cue that it can happen, and no player source relies on it. Restoring it
  would only hand sectors to bystanders by accident, so it gets no setting. A player who wants
  the sector can still order Control.
- Dropped: no

The fix changes which player owns the sector when the case arises. Decided as mandatory on
2026-09-26.
