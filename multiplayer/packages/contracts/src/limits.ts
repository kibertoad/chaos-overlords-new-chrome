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
  /** Serialized `gameSettings` object the server stores verbatim for clients. */
  gameSettingsBytes: 8 * 1024,
  /** One player's order document per turn. */
  ordersMaxOps: 512,
  opNameLength: 64,
  opArgsMaxKeys: 16,
  opArgKeyLength: 32,
  opArgStringLength: 256,
  ordersBytes: 256 * 1024,
  /** A base64-encoded native snapshot. 768 KiB raw fits a D1 row after encoding. */
  snapshotBase64Bytes: 1024 * 1024,
  /** Event log page size for the REST fallback. */
  eventsPageSize: 200,
} as const
