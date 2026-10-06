import type { ServedSeatView } from '@chaos-overlords/contracts'
import { ConflictError } from '../domain/errors'
import type { Principal } from './AuthService'
import type { KernelDeps } from './deps'
import { previousTurnConfirmed, requireParticipant, requireRunning } from './guards'
import type { Referee } from './Referee'
import type { TurnService } from './TurnService'

/**
 * Serves each seat of a match played from views its own view of the open turn (docs/MULTIPLAYER.md,
 * "Serving the views"): the match as the original shows that player at the planning entry, which
 * the client plans on in place of the whole match. Views are not stored; each request projects one
 * from the resolver's copy of the match.
 */
export class SeatViewService {
  constructor(
    private readonly deps: KernelDeps,
    private readonly turns: TurnService,
    private readonly referee: Referee,
  ) {}

  /**
   * The caller's view of the open turn.
   *
   * The turn before it must be resolved and confirmed. The seal normally does that before anyone
   * asks; when the resolver failed at the seal, this call resolves it, so a client asking again is
   * what carries a match on once the resolver is back. Until then the answer is `view_not_ready`,
   * which a client retries.
   */
  async seatView(principal: Principal): Promise<ServedSeatView> {
    const { match, player } = principal
    requireParticipant(player)
    if (!match.seatViews) {
      throw new ConflictError('This match is not played from views', {
        reason: 'not_a_view_match',
      })
    }
    if (match.status === 'finished') {
      throw new ConflictError('The match has ended; its whole state is released', {
        reason: 'match_finished',
      })
    }
    requireRunning(match)
    const number = match.currentTurn
    if (!(await previousTurnConfirmed(this.deps.storage.turns, match))) {
      await this.turns.resolveOnServer(match, number - 1)
      if (!(await previousTurnConfirmed(this.deps.storage.turns, match))) throw viewNotReady()
    }
    const outcome = await this.referee.seatView(match, number, player.slot)
    if (outcome.kind === 'served') {
      return {
        turn: outcome.view.turn,
        slot: player.slot,
        formatVersion: outcome.formatVersion,
        sessionVersion: match.sessionVersion,
        body: outcome.view.body,
      }
    }
    if (outcome.kind === 'none') {
      throw new ConflictError('Your seat is out of the match and has nothing to plan', {
        reason: 'seat_out',
      })
    }
    throw viewNotReady()
  }
}

/** A view that cannot be served yet: the turn before is unresolved, or the resolver failed. */
function viewNotReady(): ConflictError {
  return new ConflictError('The server has not resolved the turn yet; ask again shortly', {
    reason: 'view_not_ready',
  })
}
