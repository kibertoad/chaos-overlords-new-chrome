# Recreation-native save format

Status: implemented format version 1
Last updated: 2026-09-09

This format belongs to the recreation. It is deliberately separate from the
two partially mapped original *Chaos Overlords* save variants and makes no
claim of binary compatibility with either of them.

## Container and limits

- UTF-8 JSON with camel-case property names and `formatVersion: 1`.
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

## Version 1 document

The top-level members are:

| Member | Contents |
|---|---|
| `formatVersion` | Schema discriminator; currently `1` |
| `definitionsSha256` | Gameplay-definition compatibility fingerprint |
| `stateSha256` | Canonical authoritative-state fingerprint |
| `setup` | Scenario, duration, initial seed, ordered player definitions |
| `players` | Cash/support/objective state, gangs, hire state, research, inventory, statistics |
| `sectors` | Ownership, tolerance, chaos/crackdown/importance, and three site instances |
| `runtime` | Phase coordinator, RNG state/count, command queue/counter, event history/counter, notification queues/counters, phase hashes, and outcome |

Gang command projections are reconstructed from the authoritative command queue
on load. Transient `Last*Resolutions` views are intentionally not serialized;
their durable facts already exist in events and the authoritative state.

Collections whose order is mechanically meaningful retain it. Sets and maps
are emitted in key order, making a load/save cycle byte-stable for an unchanged
snapshot.

## Compatibility policy

Readers reject unknown versions until an explicit migration is implemented and
tested. A future writer must increment `formatVersion` for any incompatible
shape or semantic change, retain a fixture for version 1, and migrate into the
current in-memory model without changing the stored deterministic continuation.

Original-save import/export remains a separate research task. Native snapshots
must never be presented as converted original saves.
