# Project decisions

Status: active
Last updated: 2026-09-18

This log records deliberate product and compatibility boundaries that affect the
implementation plan.

## 2026-09-18 — Scope the Sector workspace's opponent gang view to detection

- Decision: the detailed-sector portrait strip marks an opponent with a red
  `GANGS` banner and lends the gang cards to that opponent's roster only for
  gangs the viewer already detects. The borrowed roster is per-visit state: the
  viewer's own portrait, moving the selected sector, or re-entering the screen
  restores their own gangs, and hovering an opponent's card no longer highlights
  what its queued command targets.
- Evidence: the original sector screen listed only the viewing overlord's gangs
  and carried no portrait-strip affordance for reading another overlord's, so no
  native layout or handler constrains this addition.
- Reason: the roster answers the question the sector screen already poses —
  who else is standing here — without widening what a player knows. Reusing
  `MatchState.CanPlayerDetectGang` keeps the strip, the cards, and attack
  targeting on one detection rule, and keeping the enemy action strip and
  queued-command highlight suppressed keeps orders private.
- Compatibility boundary: recreation-only presentation. It reads match state and
  queues nothing, so replays, saves, and the multiplayer protocol are unaffected.

## 2026-09-17 — Do not substitute rejected recovered AI commands

- Decision: once Original-policy preparation has produced a recovered family
  action and target, failure to find an equivalent modern legal command leaves
  that gang without a submitted command. It must not fall through to the
  recreation-native scalar scorer.
- Evidence: native family handlers write directly into the 16-byte planning
  records consumed by the resolver and contain no validator-rejection or
  alternate-command branch. The shared sector selector can legitimately return
  the source sector when capacity blocks every routed step, producing a native
  same-sector Move that the recreation's player-facing adjacency validator does
  not expose.
- Reason: inventing a different legal action changes strategy, RNG-independent
  outcomes, and later action history without original evidence. Retaining the
  exact prepared tuple while submitting nothing preserves its continuation and
  replay state without weakening validation for player commands.
- Compatibility boundary: this is a projection boundary, not a claim that the
  native game left the gang idle internally. The original retained and resolved
  its raw tuple; the recreation records that tuple in AI planning state but
  omits an unrepresentable command from the modern queue.

## 2026-09-17 — Correct the original registry persistence defects

- Decision: retain the recreation's validated, atomic, per-user preferences file
  rather than reproduce the executable's machine-wide registry implementation.
- Evidence: loader `0x0046439a` and writer `0x00464783` both open
  `HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0` with `0x20019`
  (`KEY_READ`). The writer then issues twelve `RegSetValueExA` calls without a
  handle carrying `KEY_SET_VALUE`; the loader attempts the same for a generated
  `serialNum`. Every result is ignored. The loader also reuses one DWORD across
  thirteen unchecked queries, allowing a missing later value to inherit stale
  data instead of its compiled default.
- Reason: silent non-persistence and cross-setting contamination are clear API
  misuse, not game design. Reproducing them would lose user choices and make
  malformed legacy state affect unrelated settings.
- Compatibility boundary: original compiled defaults and option semantics remain
  evidence for recreation defaults, except for separately documented modern
  choices such as Slide Panels off. Preference writes are reliable and bounded;
  malformed data falls back per field. The obsolete `serialNum` side effect is
  excluded from authoritative RNG, as documented in `BIN-RNG-001`.

## 2026-09-17 — Correct the original AI hire slot/role indexing defect

- Decision: when the AI hire scheduler considers its scenario-specific family-6
  slot, compare the previous hire role with role 4 in every scenario instead of
  copying the executable's comparisons with slot numbers 6, 5, 2, 10, and 5.
- Evidence: selector `0x8f` in `0x00402d70` reads `0x00482160`, and planner
  `0x00458fa0` fills that location from current-role array `0x00482128` before
  choosing the next role. Each guarded special slot writes role 4, which the
  recovered scenario/family table maps to family 6. Dominance's original
  comparison with 10 is impossible for the verified 0..6 role domain; the other
  values can only suppress unrelated roles by numerical coincidence.
- Reason: this is a clear slot-versus-role indexing defect rather than ambiguous
  game design. Preserving it would create arbitrary scenario-dependent repeated
  family-6 hiring. The corrected comparison implements the common apparent
  intent—do not immediately choose another family-6 hire—while leaving all
  schedule tables, quotas, availability tests, ranking, and RNG behavior intact.
- Compatibility boundary: the `Original` policy deliberately differs from the
  shipped executable at this guard. Static evidence and tests preserve both the
  original finding and the exact recreation exception; no claim of bit-for-bit
  AI decision parity includes this bug.

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

- Decision: the logo and intro movies play unattended only until one run has
  reached the end of the queue. The recreation has no player profiles, so the
  record lives in the single local preferences file alongside the other client
  settings. Completing it records `IntroMoviesSeen` in
  the client preferences (format v8), and the title screen gains an `INTRO`
  button that replays the same queue on demand.
- Reason: 135 seconds of startup video, even with skip input, is a toll on every
  launch of a recreation that players restart often, while the movies themselves
  are content worth keeping reachable.
- Boundary: the flag is presentation preference only. It never enters saves,
  replays, phase hashes, or multiplayer state, a failed preference write leaves
  playback unaffected, and an unreadable movie pack reports on the title screen
  instead of blocking it.

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
  itself and a load resumes it when it ends at that save's own state. An old
  save still loads, a missing or corrupt journal is never a failed load, and a
  slot saved without one loses the journal already there rather than pairing
  with another game's history.
- A load adopts the journal rather than replaying it. Re-deriving the state from
  the steps means re-running the whole match — every recorded operation plus a
  full-state fingerprint each — on the thread the player is waiting on: a
  30-turn match measured 147 ms, and it grows with the match, so the reward for
  a long session would be a load that visibly stops. The save already *is* that
  state, so what the journal supplies is the history, and the one thing worth
  proving is that the two belong together: the last step's fingerprint against
  the restored state's. That is the same equality the replay was reduced to at
  the end, and it is the recorder's own invariant, so a companion left by
  another game in the same slot is still refused. Adopting measures 7 ms. Where
  something needs every step to still reproduce — a bug report, which replays
  the journal to anonymize it — that check happens there, off the game loop and
  on the copy about to be sent.
- Compression is Brotli, not zstd, with the codec byte reserved for zstd. A
  journal is the opening snapshot plus every recorded command, each with the
  fingerprint of the state it produced; the commands are most of the bytes and
  almost none of the compressed size, and the fingerprints are the reverse. In a
  measured 27-turn match the step array went from 106 KB to 12.6 KB, of which
  12.3 KB was the fingerprints and 0.2 KB everything else — and 364 hashes carry
  11.6 KB of entropy, so Brotli is already within a few percent of the floor.
  That makes the codec a question of what each side already has rather than of
  ratio: Brotli ships in .NET, in Node and in a Cloudflare Worker under
  `nodejs_compat`, while zstd needs a package on the game side and has no
  Workers decoder. A new dependency in a game that must build offline is the
  larger cost.
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
