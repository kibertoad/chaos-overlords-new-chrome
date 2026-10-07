# DEV-HIRE-003

- Departs from: RULE-HIRE-001
- Reason: The rebuild lets a hire whose cost is 0 through while the player's cash is negative.
  The original was read as failing a hire when its cost is greater than cash, which would refuse
  that hire. The Dropped item corrects that reading.
- Setting: None
- Default: mandatory
- Justification: A hire that costs nothing takes nothing from cash, so refusing it because cash is
  already negative protects nothing and only keeps a player in debt from rebuilding. It adds an
  option and removes none.
- Dropped: 2026-09-25, the original also lets a zero-cost hire through while cash is negative: it
  skips the cash test when the cost is 0 (FND-HIRE-006, RULE-HIRE-001). The earlier reading
  missed the zero-cost branch in front of the cash test.
