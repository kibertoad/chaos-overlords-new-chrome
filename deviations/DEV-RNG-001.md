# DEV-RNG-001

- Departs from: RULE-RNG-001, RULE-OPTIONS-001
- Reason: The run's sequence is seeded from the low 16 bits of the rebuild's own uptime clock when
  the game object is created, in place of `timeGetTime` at process start. Later local games and
  loads draw on from it, as in the original. Replays, tests and online
  matches take an explicit seed of full width. The rebuild never makes the options loader's two
  `serialNum` draws, so an installation-wide value cannot shift a match's random sequence.
- Setting: None
- Default: mandatory
- Justification: A player cannot tell one random sequence from another, and leaving out the draws
  stops an installation-wide value from shifting a match, so a seed reproduces its match on any
  installation. The original's seed, the time since Windows started, cannot be reproduced anyway. A
  test that replays a fixture from its seed rather than a recorded generator state makes the
  original's six draws itself and cites this entry.
- Dropped: no
