# DEV-VIDEO-002

- Departs from: RULE-VIDEO-001
- Reason: A press of Escape, Enter or Space, or of either mouse button, ends the movie playing,
  and the press is consumed. The original ends a movie only when the left button is held down at
  one of its 100 ms input ticks and ignores the keyboard. While the window is inactive the rebuild
  holds the movie where it is; the original plays on.
- Setting: None
- Default: mandatory
- Justification: This is an interface change that removes friction: a click the original could
  miss between two ticks always takes effect, and the usual keys work. It changes no rule and
  nothing a match starts from, since the movies end before the title screen either way. A setting
  to bring back the missed clicks would give a player nothing.
- Dropped: no
