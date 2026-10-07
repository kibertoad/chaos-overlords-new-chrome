# DEV-VIDEO-001

- Departs from: FMT-VIDEO-001, RULE-VIDEO-001
- Reason: The rebuild decodes the two shipped movies with its own decoder for the subset of the
  format they use, where the original calls the Smacker library. A file that is malformed or uses
  anything outside that subset is skipped and play goes on to the next movie or the title screen,
  as a file the original's library fails to open is skipped.
- Setting: None
- Default: mandatory
- Justification: It changes only what happens where the original would fail to play the file, so
  there is nothing after that point to keep or compare.
- Dropped: no

Decided 2026-09-13.
