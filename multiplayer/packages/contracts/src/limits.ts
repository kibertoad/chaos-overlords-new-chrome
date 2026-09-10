/**
 * Hard limits shared by validation on the server and by clients that want to refuse a payload
 * before sending it. Every bounded thing on the wire is bounded here, once.
 */
export const LIMITS = {
  /** Players per match, matching the six slots of the original game. */
  maxPlayers: 6,
  minPlayers: 2,
  matchNameLength: 64,
  displayNameLength: 32,
  passwordMinLength: 6,
  passwordMaxLength: 128,
  joinCodeLength: 8,
  /** Turn timer bounds in seconds; 0 disables the timer. */
  turnTimerMinSeconds: 30,
  turnTimerMaxSeconds: 86_400,
  /** Serialized `gameSettings` object the server stores verbatim for clients, in UTF-8 bytes. */
  gameSettingsBytes: 8 * 1024,
  /** Ops in one player's order document for a turn. */
  ordersMaxOps: 512,
  ordersBytes: 256 * 1024,
  /** Nesting allowed inside the opaque `gameSettings` object, so a deep body cannot blow the stack. */
  gameSettingsMaxDepth: 16,
  /** A base64-encoded native snapshot. 768 KiB raw fits a D1 row after encoding. */
  snapshotBase64Bytes: 1024 * 1024,
  /** Event log page size for the REST fallback. */
  eventsPageSize: 200,
} as const

/**
 * The ceiling on every counter the wire carries without a domain limit of its own: turn numbers,
 * event sequence numbers, save format versions, gang ids.
 *
 * It is `int.MaxValue` because the C# client holds each of them in an `int`, and a value the
 * client cannot deserialize is a match it drops out of rather than a number it rounds. Stating the
 * bound also lets the generated C# use `int` at all: a JavaScript integer runs to 2^53, so an
 * unbounded one has to be a `long` on the other side.
 */
export const INT32_MAX = 2_147_483_647

/** `MatchSetup.InitialSeed` is a C# `int`, so half of all seeds are negative. */
export const INT32_MIN = -2_147_483_648
