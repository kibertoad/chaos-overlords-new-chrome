# Recreation-native save format

Status: implemented format version 2
Last updated: 2026-09-09

This format belongs to the recreation. It is deliberately separate from the
two partially mapped original *Chaos Overlords* save variants and makes no
claim of binary compatibility with either of them.

## Container and limits

- UTF-8 JSON with camel-case property names and `formatVersion: 2`.
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

## Version 2 document

The top-level members are:

| Member | Contents |
|---|---|
| `formatVersion` | Schema discriminator; currently `2` |
| `definitionsSha256` | Gameplay-definition compatibility fingerprint |
| `stateSha256` | Canonical authoritative-state fingerprint |
| `setup` | Scenario, duration, initial seed, ordered player definitions |
| `players` | Cash/support/objective state, gangs, hire state, research, inventory, statistics |
| `sectors` | Ownership, explicit base income, tolerance, chaos/crackdown/importance, and three site instances |
| `runtime` | Phase coordinator, RNG state/count, command queue/counter, event history/counter, notification queues/counters, phase hashes, and outcome |

Gang command projections are reconstructed from the authoritative command queue
on load. Transient `Last*Resolutions` views are intentionally not serialized;
their durable facts already exist in events and the authoritative state.

Collections whose order is mechanically meaningful retain it. Sets and maps
are emitted in key order, making a load/save cycle byte-stable for an unchanged
snapshot.

## Compatibility policy

Readers accept version 1 and migrate its formerly implicit sector income from
the sum of each sector's three site cash values. The legacy version-4 canonical
hash is verified before the migrated state is returned. Unknown versions remain
rejected. Future incompatible changes must increment `formatVersion`, retain
fixtures, and preserve deterministic continuation during migration.

Original-save import/export remains a separate research task. Native snapshots
must never be presented as converted original saves.

## Replay format version 2

`MatchReplayRecorder` captures an initial native snapshot, then requires every
authoritative mutation to pass through its API. It covers command submission and
cancellation, hire selection and snubbing, all phase transitions, and
notification dismissal. Before recording or saving, it verifies that the match
has not been mutated out of band.

Each ordered replay step stores its operation payload, the expected validation
result where applicable, and the canonical state SHA-256 after the operation.
`MatchReplaySerializer.LoadAndReplay` restores the initial snapshot, repeats the
operations, checks validation outcomes, and rejects the file at the first hash
divergence. Replay input is limited to 32 MiB and 1,000,000 operations. Unknown
members, missing values, unknown versions, and malformed operations are rejected.
`MatchReplayStore` provides same-directory temporary-file promotion for replay
files. The prototype client records all of its mutations and exposes atomic
save plus verified playback through F6 and F10.

Version 2 embeds native-save version 2 and uses canonical state hash version 5,
which includes explicit sector income. The initial snapshot remains required
until original seed selection and the complete setup context are verified.
