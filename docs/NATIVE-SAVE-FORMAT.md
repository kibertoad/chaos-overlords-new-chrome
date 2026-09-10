# Recreation-native save format

Status: implemented format version 8
Last updated: 2026-09-10

This format belongs to the recreation. It is deliberately separate from the
two partially mapped original *Chaos Overlords* save variants and makes no
claim of binary compatibility with either of them.

## Container and limits

- UTF-8 JSON with camel-case property names and `formatVersion: 8`.
- Maximum accepted size: 16 MiB.
- Unknown properties, missing constructor fields, invalid identifiers, invalid
  enum/phase combinations, and inconsistent sequence counters are rejected.
- `definitionsSha256` fingerprints the complete supplied site/gang/item model;
  loading with different gameplay data is rejected.
- `stateSha256` is the canonical `MatchStateHasher` digest captured at save
  time and verified after reconstruction.

`NativeSaveSerializer` reads and writes streams. `NativeSaveStore` writes a
same-directory temporary file, flushes it to disk, retains the previous primary
as `<save>.bak`, and promotes the temporary file over the primary. Recovery
loads the backup only when the primary is missing, unreadable, or invalid.

## Version 8 document

The top-level members are:

| Member | Contents |
|---|---|
| `formatVersion` | Schema discriminator; currently `8` |
| `definitionsSha256` | Gameplay-definition compatibility fingerprint |
| `stateSha256` | Canonical authoritative-state fingerprint |
| `setup` | Scenario, duration, initial seed, global AI mentality, and ordered player definitions including portrait IDs |
| `players` | Cash/support/objective state, gangs, three fixed hire slots and pending action slot, research, inventory, statistics |
| `sectors` | Ownership, explicit base income, tolerance, chaos/crackdown/importance, and three site instances |
| `runtime` | Phase coordinator, RNG state/count, fixed-six-player AI reactions/directional attitudes, six current and previous AI hire roles, six-by-81 AI family records, command queue/counter, event history/counter, notification queues/counters, phase hashes, and outcome |

Gang command projections are reconstructed from the authoritative command queue
on load. Transient `Last*Resolutions` views are intentionally not serialized;
their durable facts already exist in events and the authoritative state.

Collections whose order is mechanically meaningful retain it. Sets and maps
are emitted in key order, making a load/save cycle byte-stable for an unchanged
snapshot.

## Compatibility policy

Readers accept versions 1 through 7. Older documents migrate formerly implicit
sector income and later Crackdown state according to their schema; all v1-v4
setups migrate to Criminal AI mentality and map each player to its matching
default portrait. Version 5 and earlier reconstruct the fixed AI reaction and
attitude state without advancing the saved live RNG. Version 6 and earlier
initialize the newly authoritative hire roles to zero and all planning-family
slots to the original sentinel 99. Version 7 compact hire pools map
deterministically into the fixed slots introduced in version 8. Mid-action
saves retain removed definitions and any already-drawn premature replacement in
an explicit compatibility field until resolution. The appropriate legacy
canonical hash is verified before the migrated state is returned. Unknown
versions remain rejected. Future incompatible changes must increment
`formatVersion`, retain fixtures, and preserve deterministic continuation
during migration.

Original-save import/export remains a separate research task. Native snapshots
must never be presented as converted original saves.

## Replay format version 9

`MatchReplayRecorder` captures an initial native snapshot, then requires every
authoritative mutation to pass through its API. It covers command submission and
cancellation, hire selection and snubbing, all phase transitions, and
notification dismissal. Before recording or saving, it verifies that the match
has not been mutated out of band.

Version 8 added the deterministic post-command AI hiring-preparation operation,
which updates the authoritative current hire role before offer selection.
Version 9 embeds native snapshot version 8 and fingerprints fixed hire-slot and
pending action-slot state.

Each ordered replay step stores its operation payload, the expected validation
result where applicable, and the canonical state SHA-256 after the operation.
`MatchReplaySerializer.LoadAndReplay` restores the initial snapshot, repeats the
operations, checks validation outcomes, and rejects the file at the first hash
divergence. Replay input is limited to 32 MiB and 1,000,000 operations. Unknown
members, missing values, unknown versions, and malformed operations are rejected.
`MatchReplayStore` provides same-directory temporary-file promotion for replay
files. The prototype client records all of its mutations and exposes atomic
save plus verified playback through F6 and F10.

Replay version 3 embeds a native-save version 4 initial snapshot and records
planning-time hire-offer preparation so opening the persistent Hire dock does
not become an out-of-band RNG mutation. Replay version 4 embeds the version 5
snapshot and includes global AI mentality and player portraits in the canonical
state. Replay version 5 embeds the version 6 snapshot and includes AI reactions
and attitudes. Replay version 7 embeds the version 7 snapshot and includes AI
hire roles and planning families. Replay version 8 retained that native snapshot
and recorded post-command AI hiring preparation. Replay version 9 embeds native
version 8 with fixed hire slots. Versions 7 and 8 use their version-10 hash
path, while version 6 uses version 9. Version 2 through 5 replay documents
remain accepted through their legacy hash paths. The current canonical state hash is version
11. Initial-state migration is covered for replay version 8; checked-in legacy
hire-operation streams remain pending. The initial snapshot
remains required until original seed selection and the complete setup context
are verified.
