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
- Status: bounded container, palette, packed-audio, and indexed-frame decoding
  plus streaming presentation are implemented. The recreation policy is tested;
  original trigger/skip evidence and native visual/audio fidelity remain pending.

## 2026-09-13 — Stream the intro once, then keep it on the title screen

- Decision: the logo and intro movies play unattended only until a profile has
  reached the end of the queue once. Completing it records `IntroMoviesSeen` in
  the client preferences (format v8), and the title screen gains an `INTRO`
  button that replays the same queue on demand.
- Reason: 135 seconds of startup video, even with skip input, is a toll on every
  launch of a recreation that players restart often, while the movies themselves
  are content worth keeping reachable.
- Boundary: the flag is presentation preference only. It never enters saves,
  replays, phase hashes, or multiplayer state, a failed preference write leaves
  playback unaffected, and an unreadable movie pack reports on the title screen
  instead of blocking it.
