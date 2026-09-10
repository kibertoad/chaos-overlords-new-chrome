# Project decisions

Status: active
Last updated: 2026-09-10

This log records deliberate product and compatibility boundaries that affect the
implementation plan.

## 2026-09-10 — Save compatibility scope

- Importing or exporting original *Chaos Overlords* saves is an explicit
  non-goal.
- Recreation-native saves and replays may change incompatibly before version
  1.0.0. Compatibility between pre-1.0 development formats is useful but is not
  a release gate.
- Keep the existing version discriminator, bounded readers, legacy hash
  selection, and migration structure as infrastructure for post-1.0 evolution.
- Existing pre-1.0 readers and tests may be retained when inexpensive, but new
  schema work may remove or replace them rather than accumulate migration debt.
- Starting with 1.0.0, incompatible format changes must increment the format
  version and provide either a deterministic migration or an explicitly
  documented safe rejection path.

## 2026-09-10 — Networking scope

Original and modern networked multiplayer are outside the parity target. The
supported multiplayer mode is local hot-seat play.
