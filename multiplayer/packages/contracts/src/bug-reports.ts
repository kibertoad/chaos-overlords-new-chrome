import {
  boolean,
  check,
  type InferOutput,
  integer,
  maxLength,
  maxValue,
  minLength,
  minValue,
  number,
  optional,
  picklist,
  pipe,
  regex,
  strictObject,
  string,
  trim,
} from 'valibot'
import {
  isoTimestampSchema,
  notNegativeZero,
  resourceIdSchema,
  sha256HexSchema,
} from './primitives'

/**
 * What a player may send when something went wrong, and what the server answers.
 *
 * A report is two things: what the player typed, and — only if they left the box ticked — the
 * match as an event-sourced journal that replays from its first turn. The journal is the whole
 * point: a description of a bug in a deterministic simulation is a guess, and the journal is the
 * bug itself, reproducible on a developer's machine.
 *
 * Nothing here is authenticated. A bug report is not a match, the player sending one has no seat,
 * and requiring a token would mean the reports worth having most — the ones from a player who
 * cannot get into a match at all — are the ones that cannot be sent. The route carries its own
 * tight rate limit instead.
 */

/**
 * Bounds for everything a report carries.
 *
 * `stateBase64Bytes` is the ceiling on the *encoded* journal and is generous on purpose: it is a
 * compressed archive of a whole match, and a long game legitimately produces a big one. The two
 * limits below it are what decide where those bytes come to rest — see
 * {@link bugReportStateSchema}.
 */
export const BUG_REPORT_LIMITS = {
  /** What the player typed. Four thousand characters is a page and a half of prose. */
  messageLength: 4_000,
  /** A version, a platform, a scenario name: identification of the build, never of the player. */
  labelLength: 128,
  /** The compressed journal, base64-encoded. 8 MiB encoded is ~6 MiB of archive. */
  stateBase64Bytes: 8 * 1024 * 1024,
  /**
   * The largest archive a deployment without a blob store will keep in its database row.
   *
   * D1 caps a row at 2 MB and a self-hosted SQLite file grows with every report, so a deployment
   * with nowhere else to put the bytes refuses a journal larger than this rather than writing one
   * it cannot store. Configure object storage (R2, or a directory on a self-hosted server) and the
   * cap stops applying — see `docs/MULTIPLAYER.md`.
   */
  inlineStateBytes: 256 * 1024,
} as const

/**
 * How the journal was compressed.
 *
 * The server never decompresses one, so this is metadata for whoever replays it later rather than
 * an instruction to the server. `zstd` is listed because the archive format reserves the codec byte
 * for it; the shipping client writes `brotli`, which .NET, Node and `nodejs_compat` Workers all
 * decode without a dependency. `none` exists for a test fixture and for an archive small enough
 * that compressing it is noise.
 */
export const bugReportCodecSchema = picklist(['none', 'brotli', 'zstd'])

export type BugReportCodec = InferOutput<typeof bugReportCodecSchema>

/** How the match was being played, which is often the whole reproduction. */
export const bugReportMatchTypeSchema = picklist(['single', 'hotseat', 'online'])

/** A bounded, non-identifying string: a build version, a platform name, a scenario. */
export const bugReportLabelSchema = pipe(
  string(),
  trim(),
  minLength(1),
  maxLength(BUG_REPORT_LIMITS.labelLength),
)

/** A count a report carries for triage: a turn number, a number of seats. */
export const bugReportCountSchema = pipe(
  number(),
  integer(),
  minValue(0),
  maxValue(2_147_483_647),
  notNegativeZero,
)

/**
 * The build the report came from.
 *
 * Deliberately two fields and no more. A crash report that carries a file path carries the
 * player's user name in it, and a report nobody can safely read is a report nobody reads.
 */
export const bugReportBuildSchema = strictObject({
  version: bugReportLabelSchema,
  platform: bugReportLabelSchema,
})

/**
 * Where the player was when they hit the button, for triage before anything is replayed.
 *
 * Every field is a category or a count. Names are not here, and are not in the journal either
 * unless the player sent one without anonymising it, which the client does not offer.
 */
export const bugReportContextSchema = strictObject({
  scenario: bugReportLabelSchema,
  matchType: bugReportMatchTypeSchema,
  turn: bugReportCountSchema,
  phase: bugReportLabelSchema,
  humanPlayers: bugReportCountSchema,
  computerPlayers: bugReportCountSchema,
})

/**
 * The attached journal: a compressed, self-verifying archive the server stores and never opens.
 *
 * `sha256` is over the *compressed* bytes, so the server can tell a truncated upload from a good
 * one without decoding anything, and a developer can tell the archive they downloaded is the
 * archive that was sent. `uncompressedBytes` is what the archive expands to, which is what decides
 * whether replaying it is a second or a minute.
 */
export const bugReportStateSchema = strictObject({
  codec: bugReportCodecSchema,
  /** `MatchReplaySerializer.CurrentFormatVersion` at the time of the report. */
  replayFormatVersion: pipe(
    number(),
    integer(),
    minValue(1),
    maxValue(2_147_483_647),
    notNegativeZero,
  ),
  uncompressedBytes: bugReportCountSchema,
  sha256: sha256HexSchema,
  /** Whether the client rewrote player names and Comlink text before compressing. */
  anonymized: boolean(),
  body: pipe(
    string(),
    maxLength(BUG_REPORT_LIMITS.stateBase64Bytes),
    regex(/^[A-Za-z0-9+/]*={0,2}$/, 'expected standard base64'),
    check((value) => value.length % 4 === 0, 'base64 length must be a multiple of four'),
  ),
})

export const submitBugReportRequestSchema = strictObject({
  message: pipe(string(), trim(), minLength(1), maxLength(BUG_REPORT_LIMITS.messageLength)),
  client: bugReportBuildSchema,
  context: optional(bugReportContextSchema),
  state: optional(bugReportStateSchema),
})

export type SubmitBugReportRequest = InferOutput<typeof submitBugReportRequestSchema>

/**
 * What the player gets back.
 *
 * `stateStored` is not decoration: a deployment with nowhere to put a large archive still accepts
 * the report, and the player is entitled to know the journal did not come with it so they can say
 * so in a follow-up rather than assume it was read.
 */
export const bugReportReceiptSchema = strictObject({
  id: resourceIdSchema,
  receivedAt: isoTimestampSchema,
  stateStored: picklist(['stored', 'omitted', 'not_sent']),
})

export type BugReportReceipt = InferOutput<typeof bugReportReceiptSchema>
