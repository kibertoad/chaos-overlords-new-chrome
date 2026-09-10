import type { Match, Player } from '../domain/entities'
import { UnauthorizedError } from '../domain/errors'
import { hashToken, TOKEN_PREFIX } from '../logic/crypto'
import type { MultiplayerStorage } from '../ports/storage'

export interface Principal {
  player: Player
  match: Match
}

export class AuthService {
  constructor(private readonly storage: MultiplayerStorage) {}

  /** Resolves a bearer token to its player and match. Never reveals which half failed. */
  async authenticate(token: string): Promise<Principal> {
    if (!token.startsWith(TOKEN_PREFIX)) throw invalid()
    const player = await this.storage.players.getByTokenHash(await hashToken(token))
    if (!player) throw invalid()
    const match = await this.storage.matches.get(player.matchId)
    if (!match) throw invalid()
    return { player, match }
  }
}

function invalid(): UnauthorizedError {
  return new UnauthorizedError('Invalid or expired player token', { reason: 'invalid_token' })
}
