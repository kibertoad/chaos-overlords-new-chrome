# DEV-AUDIO-001

- Departs from: RULE-AUDIO-010
- Replaces: RULE-AUDIO-010
- Reason: The rebuild does not run the original's startup drive check or its unused search of the
  CD drives. It plays the music tracks from the files of the GOG release (`MUSIC/TrackNN.ogg`) and
  never looks for a drive or a disc.
- Setting: None
- Default: mandatory
- Justification: The check always passes and the search is never called, so neither changes any
  game state. Its only effect in the original, the prefix of the movie paths, is replaced by the
  rebuild's own asset paths. A setting would have nothing to switch.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs
- Dropped: no
