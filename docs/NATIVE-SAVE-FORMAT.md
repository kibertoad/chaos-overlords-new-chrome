# Recreation-native save format

Status: implemented format version 19
Last updated: 2026-09-11

This format belongs to the recreation. It is deliberately separate from the
two partially mapped original *Chaos Overlords* save variants and makes no
claim of binary compatibility with either of them.

## Container and limits

- UTF-8 JSON with camel-case property names and `formatVersion: 19`.
- Maximum accepted size: 16 MiB.
- Unknown properties, missing constructor fields, invalid identifiers, invalid
  enum/phase combinations, and inconsistent sequence counters are rejected.
- `definitionsSha256` fingerprints the complete supplied site/gang/item model;
  loading with different gameplay data is rejected.
- `stateSha256` is the canonical `MatchStateHasher` digest captured at save
  time and verified after reconstruction.

`NativeSaveSerializer` reads and writes streams. `NativeSaveStore` writes and
flushes a same-directory temporary file, reads it back through the bounded
serializer, then atomically promotes it. A valid previous primary becomes
`<save>.bak`; an invalid primary is replaced without overwriting an existing
good backup. Recovery loads the backup only when the primary is missing,
unreadable, or invalid.

## Version 19 document

The top-level members are:

| Member | Contents |
|---|---|
| `formatVersion` | Schema discriminator; currently `19` |
| `definitionsSha256` | Gameplay-definition compatibility fingerprint |
| `stateSha256` | Canonical authoritative-state fingerprint |
| `setup` | Scenario, duration, initial seed, global AI mentality, and ordered player definitions including portrait IDs |
| `players` | Cash/support/objective state, gangs, three fixed hire slots, pending action slot and legacy prepaid marker, persistent maximum-hire-Force modifier, research, inventory, statistics |
| `sectors` | Ownership, explicit base income, tolerance, chaos/crackdown/importance, and three site instances |
| `runtime` | Phase coordinator, RNG state/count, fixed-six-player AI reactions/directional attitudes, six current and previous AI hire roles, six-by-81 AI family records, three generations of action plus two command-dependent target bytes, weapon/armor planning cooldowns, polymorphic family-2/7 focus/family-11 formation values, family-6 coverage sectors, six first-planning flags, six encoded hire-placement anchors, command queue/counter with primary through quaternary targets, event history/counter, notification queues/counters, per-player Comlink inbox messages/sequences/read cursors, phase hashes, and outcome |

Gang command projections are reconstructed from the authoritative command queue
on load. Transient `Last*Resolutions` views are intentionally not serialized;
their durable facts already exist in events and the authoritative state.

Collections whose order is mechanically meaningful retain it. Sets and maps
are emitted in key order, making a load/save cycle byte-stable for an unchanged
snapshot.

## Compatibility policy

Readers currently accept versions 1 through 19. This pre-1.0 compatibility is
useful test coverage, not a product guarantee: readers and fixtures for old
development schemas may be removed or replaced when the authoritative model
changes. Older documents currently migrate formerly implicit
sector income and later Crackdown state according to their schema; all v1-v4
setups migrate to Criminal AI mentality and map each player to its matching
default portrait. Version 5 and earlier reconstruct the fixed AI reaction and
attitude state without advancing the saved live RNG. Version 6 and earlier
initialize the newly authoritative hire roles to zero and all planning-family
slots to the original sentinel 99. Version 7 compact hire pools map
deterministically into the fixed slots introduced in version 8. Mid-action
saves retain removed definitions and any already-drawn premature replacement in
an explicit compatibility field until resolution. Version 8 pending hires
migrate as already paid, preserving their prior cash and cumulative-spending
mutation without charging twice; version 9 selections remain unpaid until
successful resolution. Version 9 and earlier initialize the newly authoritative
maximum-hire-Force modifier to false; version 10 preserves it independently of
the player's name. Version 10 and earlier derive the newly authoritative
placement anchors from each restored gang-slot-zero sector; unconfigured
recreation slots use the inactive-sector encoding. Version 11 and earlier
initialize the newly authoritative three-generation AI action-byte histories
to `None`. Version 12 and earlier initialize all newly authoritative action
target bytes to zero and infer each first-planning flag from whether that
player's family/action records contain initialized data. Version 13 and earlier
initialize the weapon and armor cooldowns to zero. Version 14 and earlier infer
the polymorphic focus/formation values from active-gang sectors; this is a
development-format convenience, not a pre-1.0 compatibility promise. Version
15 and earlier initialize the newly authoritative family-6 coverage sectors to
`-1`. Version 16 and earlier initialize empty Comlink inboxes. Version 18 adds
and fingerprints the optional tertiary command target used by multi-item Sell;
version 19 adds and fingerprints the optional quaternary target needed when
multi-item Give carries a recipient plus all three equipment slots. Older
documents naturally restore the absent targets as null.
The appropriate legacy canonical hash is verified before the
migrated state is returned. Unknown
versions remain rejected. Starting with 1.0.0, incompatible changes must
increment `formatVersion` and provide either deterministic migration with
fixtures or an explicitly documented safe rejection path. The existing bounded
reader, legacy-hash selection, and migration structure is retained for that
post-1.0 policy.

Original-save import/export is an explicit non-goal. Native snapshots must never
be presented as converted original saves.

## Replay format version 20

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
multi-item Give.

Each ordered replay step stores its operation payload, the expected validation
result where applicable, and the canonical state SHA-256 after the operation.
`MatchReplaySerializer.LoadAndReplay` restores the initial snapshot, repeats the
operations, checks validation outcomes, and rejects the file at the first hash
divergence. Replay input is limited to 32 MiB and 1,000,000 operations. Unknown
members, missing values, unknown versions, and malformed operations are rejected.
`MatchReplayStore` applies the same read-back-before-promotion and
last-valid-generation backup policy to replay files. The game client
records all of its mutations and exposes atomic save plus verified primary or
backup playback through F6 and F10.

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
embeds native version 19.
Version 12 uses the version-14 hash and initializes action histories to `None`;
version 11 uses the version-13 hash and
derives placement anchors; version 10 uses the version-12 hash and migrates the
new modifier to false; version 9 uses its version-11 hash, versions 7 and 8
use version 10, and version 6 uses version 9. Version 2 through 5 replay
documents remain accepted through their legacy hash paths. Replay versions 18
and 19 retain their version-20 and version-21 hash projections respectively;
the current canonical state hash is version 22. Initial-state migration is covered for
replay version 8, and version 9 operation semantics are covered. Additional
pre-1.0 legacy fixtures are not a release gate. The initial snapshot remains
required until original seed selection and the complete setup context are
verified.
