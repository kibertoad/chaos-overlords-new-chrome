# Recreation-native save format

Status: implemented format version 27
Last updated: 2026-09-23

This format belongs to the recreation. It is deliberately separate from the
original *Chaos Overlords* fixed-memory save envelopes and makes no claim of
binary compatibility with them. Static analysis bounds the original standalone
`S40W` form at 45,305 bytes and its six-DWORD legacy-network `N40W` extension at
45,329 bytes; both repeat their header marker as a trailer but ignore every
individual Win32 I/O byte count. `BIN-API-003` records the exact block
sequence. Those facts inform state research only: this recreation uses a safe,
portable, independently versioned document rather than reproducing the
original's address-shaped layout or partial-read behavior.

<!-- doc-index:begin toc depth=2 -->
- [Container and limits](#container-and-limits)
- [Version 27 document](#version-27-document)
- [Compatibility policy](#compatibility-policy)
- [Replay format version 31](#replay-format-version-31)
<!-- doc-index:end -->

## Container and limits

- UTF-8 JSON with camel-case property names and `formatVersion: 27`.
- Maximum accepted size: 16 MiB.
- Unknown properties, missing constructor fields, invalid identifiers, invalid
  enum/phase combinations, and inconsistent sequence counters are rejected.
- `definitionsSha256` fingerprints the complete supplied site/gang/item model;
  loading with different gameplay data is rejected.
- `stateFingerprint` is the canonical `MatchStateHasher` fingerprint captured
  at save time and verified after reconstruction: 128 bits of XxHash128 over
  the canonical little-endian state encoding, as 32 lowercase hex characters.
  It is a checksum against corruption and divergence, not a cryptographic
  digest; `definitionsSha256` stays a SHA-256 because it is computed once.

`NativeSaveSerializer` reads and writes streams. `NativeSaveStore` writes and
flushes a same-directory temporary file, reads it back through the bounded
serializer, then atomically promotes it. A valid previous primary becomes
`<save>.bak`; an invalid primary is replaced without overwriting an existing
good backup. Recovery loads the backup only when the primary is missing,
unreadable, or invalid.

## Version 27 document

The top-level members are:

| Member | Contents |
|---|---|
| `formatVersion` | Schema discriminator; currently `27` |
| `definitionsSha256` | Gameplay-definition compatibility fingerprint |
| `stateFingerprint` | Canonical authoritative-state fingerprint |
| `setup` | Scenario, duration, initial seed, global AI mentality, Original/Advanced AI policy, and ordered player definitions including portrait IDs |
| `players` | Cash/support/objective state, gangs, three fixed hire slots, pending action slot and legacy prepaid marker, persistent maximum-hire-Force modifier, research, inventory, statistics |
| `sectors` | Ownership, explicit generated income, tolerance, Crackdown/importance, and three site instances |
| `runtime` | Phase coordinator, RNG state/count, fixed-six-player AI reactions/directional attitudes, six current and previous AI hire roles, six-by-81 AI family records, three generations of action plus two command-dependent target bytes, weapon/armor planning cooldowns, polymorphic family-2/7 focus/family-11 formation values, family-6 coverage sectors, six first-planning flags, six encoded hire-placement anchors, command queue/counter with primary through quaternary targets, event history/counter, notification queues/counters, per-player Comlink inbox messages/sequences/per-record read sequences and legacy read-through sequence, phase hashes, and outcome |

Gang command projections are reconstructed from the authoritative command queue
on load. Transient `Last*Resolutions` views are intentionally not serialized;
their durable facts already exist in events and the authoritative state.

Collections whose order is mechanically meaningful retain it. Sets and maps
are emitted in key order, making a load/save cycle byte-stable for an unchanged
snapshot. Restored notifications must be a contiguous suffix of their sequence
counter and carry valid phase, sector, and event references. Comlink inboxes
must contain the exact bounded suffix implied by their counter, with valid
human senders and turn numbers. A completed outcome is accepted only with valid
participants, standings and awards and exactly one matching `MatchEnded` event;
its nested collections are frozen after construction.

## Compatibility policy

Only the current format version is read. A document declaring a newer version
is refused as `NewerFormat`; one declaring an older version is refused as
`OlderFormat`. Both are reported as incompatible rather than damaged, so the
save browser leaves the file where it is instead of treating it as a corrupt
slot to overwrite.

Format 26 ended the pre-release migration ladder. Format 27 changed the
fingerprint encoding again. Every earlier version was
verified through a preserved projection of the SHA-256 state hash of its day,
and formats 21 and later also carry the whole phase-boundary history in that
hash; once the fingerprint became XxHash128, none of those documents could be
checked against their contents any more, and the game had not been released,
so there was nobody whose saves a migration would have rescued. The decision is
recorded in `DECISIONS.md` under 2026-09-21.

The first 1.0.0 release establishes the stable baseline. During the 1.x release
line, later builds must load saves from every earlier public 1.x release through
bounded, deterministic migrations covered by fixtures. A change that cannot
honor that guarantee requires a new major release. Replays authenticate each
recorded step under the rules and fingerprints of their build, so a new build
does not promise to play journals from an older release unless it retains that
verifier and its rule implementation. Both file types always retain explicit
format gates: incompatible files are identified, left untouched, and never
treated as corrupt backup candidates or silently converted. A release must
document any replay version it stops accepting before publication. This policy
is a release requirement; the current pre-1.0 reader accepts only today's
versions and does not yet implement stable-release migrations.

Original-save import/export is an explicit non-goal. Native snapshots must never
be presented as converted original saves.

## Replay format version 31

`MatchReplayRecorder` captures an initial native snapshot, then requires every
authoritative mutation to pass through its API. It covers command submission and
cancellation, hire selection and snubbing, all phase transitions, and
notification dismissal, Comlink delivery, and Comlink read-state changes. Before recording or saving, it verifies that the match
has not been mutated out of band.

Version 8 added the deterministic post-command AI hiring-preparation operation,
which updates the authoritative current hire role before offer selection.
Version 9 embeds native snapshot version 8 and fingerprints fixed hire-slot and
pending action-slot state.
Version 10 embeds native snapshot version 9, fingerprints the pending-payment
marker, and defers new hire payments until successful resolution.
Version 11 embeds native snapshot version 10 and fingerprints the persistent
maximum-hire-Force modifier.
Version 12 embeds native snapshot version 11 and fingerprints all six encoded
AI hire-placement anchors.
Version 13 embeds native snapshot version 12 and fingerprints the older,
immediately previous, and newly planned AI action bytes.
Version 14 embeds native snapshot version 13 and fingerprints all six
first-planning flags plus all three generations of command-dependent AI target
bytes.
Version 15 embeds native snapshot version 14 and fingerprints weapon and armor
planning cooldowns. Version 16 embeds native snapshot version 15 and
fingerprints all six-by-81 polymorphic family-2/7 focus/family-11 formation
values. The JSON member remains named `formationSectors` in schema version 15.
Version 17 embeds native snapshot version 16 and fingerprints all six-by-81
family-6 coverage sectors. Version 18 embeds native snapshot version 17,
fingerprints every bounded Comlink inbox, and records send/read operations.
Version 19 embeds native snapshot version 18 and hash version 21, including
tertiary command targets for multi-item Sell. Version 20 embeds native snapshot
version 19 and hash version 22, including quaternary command targets for
multi-item Give. Version 21 retains native snapshot version 19 and hash version
22, and adds the ordered `PrepareSimultaneousHireOffers` operation required by
simultaneous online turns. The operation is rejected when it is mislabeled as
version 20 or earlier instead of being retroactively accepted by an older
schema. Version 22 embeds native snapshot version 20 and canonical hash version
23, authenticating the event history after every recorded mutation. Version 23
embeds native snapshot version 21 and canonical hash version 24, authenticating
phase-boundary history as well. Version 24 embeds native snapshot version 22
and canonical hash version 25; every Comlink read operation names the one
displayed message sequence it acknowledges. Versions 18 through 23 retain the
legacy mark-all operation during playback.
Version 25 embeds native snapshot version 23 and canonical hash version 26,
authenticating the match's AI policy.
Versions 26 and 27 add online controller-transfer operations. Version 28 embeds
native snapshot version 24 and canonical hash version 27, removing synthetic
sector Chaos from newly recorded state while retaining legacy verification.

Version 30 embeds native snapshot version 26 and replaces every SHA-256 step
fingerprint with the XxHash128 state fingerprint. Version 31 embeds native
snapshot version 27 and folds gameplay definitions into a digest in each state
fingerprint. Only version 31 is played
back: a journal declaring another version is refused as `NewerFormat` or
`OlderFormat`, for the reason the compatibility policy above gives, and the
per-version operation and target boundaries the older schemas needed went with
them. Journal members `initialStateFingerprint` and `resultingStateFingerprint`
replace the `…Sha256` names.

Each ordered replay step stores its operation payload, the expected validation
result where applicable, and the canonical state fingerprint after the
operation.
The tagged operation union is exact: every kind requires its complete field set
and rejects fields belonging to another operation, even though those names are
known to the shared JSON record. Nested recipient lists are frozen on record.
`MatchReplaySerializer.LoadAndReplay` restores the initial snapshot, repeats the
operations, checks validation outcomes, and rejects the file at the first hash
divergence. Replay input is limited to 32 MiB and 1,000,000 operations. Unknown
members, missing values, unknown versions, and malformed operations are rejected.
`MatchReplayStore` applies the same read-back-before-promotion and
last-valid-generation backup policy to replay files. The game client
records all of its mutations and exposes atomic save plus verified primary or
backup playback through F6 and F10. F10 verifies the complete journal before
showing its opening state. Playback can then pause, change speed, move one step
at a time, jump to either end, and exit without changing the live match.

Replay version 3 embeds a native-save version 4 initial snapshot and records
planning-time hire-offer preparation so opening the persistent Hire dock does
not become an out-of-band RNG mutation. Replay version 4 embeds the version 5
snapshot and includes global AI mentality and player portraits in the canonical
state. Replay version 5 embeds the version 6 snapshot and includes AI reactions
and attitudes. Replay version 7 embeds the version 7 snapshot and includes AI
hire roles and planning families. Replay version 8 retained that native snapshot
and recorded post-command AI hiring preparation. Replay version 9 embeds native
version 8 with fixed hire slots. Replay version 10 embeds native version 9 and
uses deferred payment while replay version 9 retains immediate payment and its
single-action validation. Replay version 11 embeds native version 10, version
12 embeds native version 11, version 13 embeds native version 12, version 14
embeds native version 13, version 15 embeds native version 14, version 16
embeds native version 15, version 17 embeds native version 16, version 18
embeds native version 17, version 19 embeds native version 18, and version 20
embeds native version 19. Version 21 retains native version 19, and version 22
embeds native version 20, and version 23 embeds native version 21.
Version 12 uses the version-14 hash and initializes action histories to `None`;
version 11 uses the version-13 hash and
derives placement anchors; version 10 uses the version-12 hash and migrates the
new modifier to false; version 9 uses its version-11 hash, versions 7 and 8
use version 10, and version 6 uses version 9. Version 2 through 5 replay
documents were accepted through their legacy hash paths before the pre-1.0
format reset. Replay versions 18
and 19 retain their version-20 and version-21 hash projections respectively;
replay versions 20 and 21 use the preserved canonical state hash version 22.
Replay version 22 uses canonical state hash version 23; replay version 23 uses
canonical state hash version 24.
Initial-state migration is covered for
replay version 8, and version 9 operation semantics are covered. Additional
pre-1.0 legacy fixtures are not a release gate. The initial snapshot remains
required until original seed selection and the complete setup context are
verified.
