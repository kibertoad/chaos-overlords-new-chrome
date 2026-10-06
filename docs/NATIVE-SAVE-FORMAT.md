# Recreation-native save format

Status: implemented save format version 40, replay format version 53, state
fingerprint encoding version 15

This format belongs to the recreation. It is deliberately separate from the
original *Chaos Overlords* fixed-memory save envelopes and makes no claim of
binary compatibility with them. Static analysis bounds the original standalone
`S40W` form at 45,305 bytes and its six-DWORD legacy-network `N40W` extension at
45,329 bytes; both repeat their header marker as a trailer but ignore every
individual Win32 I/O byte count. `FND-PLATFORM-003` records the exact block
sequence. Those facts inform state research only: this recreation uses a safe,
portable, independently versioned document rather than reproducing the
original's address-shaped layout or partial-read behavior.

<!-- doc-index:begin toc depth=2 -->
- [Container and limits](#container-and-limits)
- [Save document](#save-document)
- [Compatibility policy](#compatibility-policy)
- [Replay format](#replay-format)
<!-- doc-index:end -->

## Container and limits

- UTF-8 JSON with camel-case property names and `formatVersion: 40`.
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

## Save document

The top-level members are:

| Member | Contents |
|---|---|
| `formatVersion` | Schema discriminator; currently `40` |
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

The decision is recorded in `DECISIONS.md` under 2026-10-06. This section is
what it requires of the code and of a release.

### Before 1.0.0

Saves and replays are development formats. A build reads only the save format,
replay format and gameplay definitions it was built with, and every change to a
document's schema, to the meaning of a stored field or to the state-fingerprint
encoding moves the format version (the coupling rule is in `AGENTS.md`, and
`StateFingerprintVersionCouplingTests` pins the numbers together). No migration
is written. A file from another build is refused as incompatible, as described
below, and the player starts a new match.

Format 26 ended the earlier migration ladder. Every version before it was
verified through a preserved projection of the SHA-256 state hash of its day,
and once the fingerprint became XxHash128 none of those documents could be
checked against their contents any more; the game had not been released, so
nobody's saves were lost. The decision is recorded in `DECISIONS.md` under
2026-09-21. What each later development version changed is in the history of
this file and of the serializers; none of those versions is read.

### From 1.0.0: saves

The first 1.0.0 release fixes a stable baseline. For the whole 1.x line:

- Every 1.x build loads every save, and every backup generation of a save,
  written by an earlier public 1.x release. A save written by a later release
  is refused as `NewerFormat`; nothing promises that an older build reads a
  newer save. The companion journal beside a save is a replay, under the rules
  for replays below.
- A change that would stop an earlier 1.x save from loading moves
  `formatVersion` and ships a migration: a pure function from the document of
  version N to the document of version N + 1, applied in sequence from the
  file's version to the current one before the members are bound, under the
  same size limits. A migration never guesses a value the old document did not
  hold; a new field gets the value the old rules implied.
- The loader verifies a save before migrating it. If the state-fingerprint
  encoding changes within 1.x, the build keeps the earlier encoding so that a
  save from an earlier release is still checked against its own fingerprint;
  the migrated state is then fingerprinted under the current encoding.
- A change to the bundled gameplay definitions is a format change for this
  purpose. An earlier 1.x save written against the earlier definition set must
  still load, with a migration that maps it onto the new set.
- Before a 1.x release ships, `tests/fixtures/stable-saves/` holds a save of
  the format it writes, added in the change that moved the format (the 1.0.0
  one is added while `version.txt` still names the last 0.x release).
  `SaveCompatibilityPolicyTests` loads every fixture there with the current
  build and, once `version.txt` reaches 1.0.0, fails while the current save
  format has no fixture, so neither a format change without a fixture nor a
  change that breaks an earlier fixture passes the release workflow's tests.
- A change that cannot honour this needs a new major version. 2.0.0 may drop
  1.x saves, and its release notes say so.

### From 1.0.0: replays

A replay is evidence as well as history: each step carries the fingerprint the
build that recorded it computed, and playing it back means running the same
rules again and arriving at the same fingerprints. Migrating the document cannot
keep that promise when the rules or the encoding changed, because the steps
would then be checked against rules that did not produce them. So:

- A build plays the replay formats whose rules and fingerprint encoding it
  still has. In practice that is its own replay format. A journal of an earlier
  format is refused as `OlderFormat` and is never converted.
- Every release whose replay format differs from the previous release's says
  so in its release notes, so a player knows to keep the earlier build to watch
  earlier replays. Release builds stay downloadable from the GitHub releases
  page.
- The companion journal beside a save follows the same rule. When it cannot be
  continued, the save still loads, and the session's journal starts again at
  that load, so a bug report filed afterwards carries the history from the
  load on.

### Files a build cannot read

A file this build cannot read is never repaired, converted or overwritten in
place, and an intact file from another build is never treated as damage.

| File | Intact, from another build or another definition set | Damaged or unreadable |
|---|---|---|
| Save slot or autosave | The browser row says `SAVED BY ANOTHER BUILD  CANNOT BE LOADED HERE` and Load refuses it. The backup generation is not consulted. Saving into the slot keeps the file as the slot's backup (`<save>.bak`). | The valid backup generation loads in its place, and the row says `RECOVERED` or `BACKUP ONLY`. With no valid backup the row says `FILE CANNOT BE READ  NOT SAFE TO OVERWRITE`. |
| Companion journal | The save loads with a new journal that starts at the load. | The same. |
| F10 replay | The console says `REPLAY FROM A NEWER VERSION`, `REPLAY FROM AN OLDER VERSION` or `REPLAY USES OTHER GAME DATA`. The backup generation is not played. | The backup generation plays if it verifies, and the viewer says why the primary did not (`REPLAY FILE DAMAGED`, `REPLAY DIVERGED AT STEP N`). With no playable backup, the console names the primary's failure. Neither file is rewritten. |

`IncompatibleSave` marks the intact case on the exception, and
`ReplayDivergence` marks a replay whose step this build's rules do not
reproduce; `ReplayFailure.Of` turns either into what the player is told.

Original-save import/export is an explicit non-goal. Native snapshots must never
be presented as converted original saves.

## Replay format

`MatchReplayRecorder` captures an initial native snapshot, then requires every
authoritative mutation to pass through its API. It covers command submission and
cancellation, hire selection and snubbing, all phase transitions, notification
dismissal, Comlink delivery and read-state changes, online seat transfers, and
the three moves a local load records (the random stream continuing from the
run's sequence, the emptied Comlink inboxes and the refreshed AI sector
records). Before recording or saving, it verifies that the match has not been
mutated out of band.

The document holds `formatVersion`, `initialStateFingerprint`, the embedded
native snapshot `initialSnapshot`, and the ordered `steps`. Each step stores its
operation payload, the expected validation result where applicable, and the
state fingerprint after the operation (`resultingStateFingerprint`). The tagged
operation union is exact: every kind requires its complete field set and
rejects fields belonging to another operation, even though those names are
known to the shared JSON record. Nested recipient lists are frozen on record.
Replay input is limited to 32 MiB and 1,000,000 operations. Unknown members,
missing values, unknown versions, and malformed operations are rejected.

`MatchReplaySerializer.LoadAndReplay` restores the initial snapshot, repeats
the operations, checks validation outcomes, and rejects the file at the first
fingerprint divergence. `MatchReplayStore` applies the same
read-back-before-promotion and last-valid-generation backup policy to replay
files as `NativeSaveStore` does to saves.

### Playback

F6 saves the session's journal to `last-match.rchreplay` in the user data
directory, and F10 opens it in the replay viewer (`DEV-UI-026`).
`MatchReplaySerializer.OpenPlayback` replays the whole journal and checks every
step's fingerprint before the first frame is shown, so the viewer can never show
a state the journal does not vouch for. `MatchReplayPlayback` is the cursor the
viewer moves:

- On the opening pass it keeps compressed native snapshots at evenly spaced
  positions, at most 64 of them and at least 32 steps apart, and a seek
  starts from the nearest one at or before its target. A state larger than the
  native save limit gets no snapshot, and a seek past it walks on from the last
  one written.
- Every step a seek applies is checked against its recorded fingerprint again.
- Before it moves, the cursor checks that the shown state still has the
  fingerprint of its position, and rebuilds it from a snapshot if the viewer
  changed it, so drawing code can never make an intact journal look diverged.

`MatchReplayStore.OpenPlaybackRecoveringBackup` opens the primary file, or its
backup generation when the primary is missing, damaged or diverges, and reports
why the primary could not be played. It reads only: neither file is repaired,
renamed or rewritten. A primary from another build is reported and never
passed over for the backup.

A journal recorded across a save and load carries the steps before the save,
the load's three recorded moves, and the play after it, and plays through all
of them (`MatchReplayPlaybackTests`). An autosave writes no journal, so a match
loaded from the autosave starts a new one.
