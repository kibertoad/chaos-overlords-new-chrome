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

## 2026-09-13 — Bug reports carry a replayable journal, stored apart from matches

- Decision: the in-game Escape menu can file a bug report to a hardcoded
  central address, and by default attaches the whole match as an event-sourced
  journal that replays from its first turn. The journal is anonymized before it
  is compressed and sent: player names become seat labels and Comlink text is
  redacted, both by re-running the match and recomputing every state
  fingerprint, so what is sent is a valid journal rather than an edited one.
  Names the original reads as cheat codes are game rules and are kept.
- Reason: a described bug in a deterministic simulation is a guess, and a
  journal is the bug itself. Recording one costs nothing — the replay recorder
  already wraps every mutation — but it only became a session's history once
  saves carried it: a load used to start a fresh recorder, so the turns that
  produced a bug were exactly what a report filed afterwards did not have.
- Companion, not a format change: a save writes `<save>.rchjournal` beside
  itself and a load resumes it when it replays to that save's own state. An old
  save still loads, a missing or corrupt journal is never a failed load, and a
  slot saved without one loses the journal already there rather than pairing
  with another game's history.
- Compression is Brotli, not zstd, with the codec byte reserved for zstd. The
  ratio on this payload is set by the incompressible per-step SHA-256s rather
  than by the codec, and Brotli ships in .NET, in Node and in a Cloudflare
  Worker under `nodejs_compat` while zstd needs a package on the game side and
  has no Workers decoder. A new dependency in a game that must build offline is
  the larger cost.
- Storage: reports go to the same deployment that hosts multiplayer, over a
  route of its own, into a separate D1 instance (a separate SQLite file when
  self-hosted) with its own migration lineage. They arrive unauthenticated,
  outlive the matches they describe, and carry other players' journals, so they
  share no schema, no lock and no blast radius with live matches. The journals
  themselves go to R2: one is hundreds of kilobytes to a few megabytes, D1
  refuses a row over 2 MB, and even the ones that fit would be dragged through
  every triage query. A deployment with no object store keeps archives under
  256 KiB inline and accepts the report without the journal above that.
- Boundary: the server never decompresses or parses an archive. It verifies the
  digest the client computed over the compressed bytes and stores opaque bytes.
- Status: implemented and tested. The central address is a placeholder
  (`http://localhost:8787`) until the public deployment exists.
