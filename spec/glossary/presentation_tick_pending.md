# presentation_tick_pending

Set by each `presentation_tick` and cleared by the loop that consumes it. Any
other value the game keeps: the flag byte of timer slot 0 at `0x00494810`,
set to 1 by the timer callback, so ticks a busy loop misses are lost
[FND-UI-001, FND-UI-023].
