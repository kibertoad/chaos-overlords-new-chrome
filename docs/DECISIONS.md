# Project decisions

Status: active
Last updated: 2026-09-13

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

Original network code and protocols are outside the parity target and are never
reproduced. Modern online play is a new design: a coordination server under
`multiplayer/` that relays sealed orders between deterministic clients and
verifies state hashes, hostable by players or run centrally
([`MULTIPLAYER.md`](./MULTIPLAYER.md)). Hot-seat play remains the local mode
and the client-side wiring of online play is tracked as follow-up work.

## 2026-09-13 — Decode the supported Smacker subset at runtime

- Decision: keep the two verified user-owned `.smk` files in the extracted
  pack and implement their validated Smacker-v2 subset in managed runtime code.
- Reason: this keeps asset import and playback cross-platform and deterministic
  without requiring an ambient FFmpeg executable, host codec registration, or
  thousands of derived frame files.
- Boundary: malformed/unsupported video or audio must skip presentation and
  continue to the title. Movie state is never authoritative simulation,
  persistence, replay, or multiplayer state.
- Status: the bounded header/frame-table/container validator is implemented;
  decompression, presentation, and final input-trigger parity are pending.
