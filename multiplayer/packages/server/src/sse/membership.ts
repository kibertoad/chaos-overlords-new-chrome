import type { PlayerRepository } from '@chaos-overlords/kernel'

/**
 * Whether `playerId` still holds a live membership of `matchId`: the row exists, belongs to that
 * match, and its token has not been revoked. A token is issued once per membership and never
 * rotated, so a non-null hash is the token the stream was authenticated with.
 *
 * The stream route runs this right after subscribing and every hub repeats it on its catch-up
 * heartbeat; one definition keeps the two from drifting apart.
 */
export async function isActiveMember(
  players: PlayerRepository,
  matchId: string,
  playerId: string,
): Promise<boolean> {
  const player = await players.get(playerId)
  return player?.matchId === matchId && player.tokenHash !== null
}
