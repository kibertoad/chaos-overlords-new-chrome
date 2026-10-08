import type { MatchEventBody } from '@chaos-overlords/contracts'
import type { KernelDeps } from './deps'
import type { EventPublisher } from './EventPublisher'

/** A `turn.desynced` payload as this build writes it: the tie-breaker is always named. */
export type DesyncPayload = Extract<MatchEventBody, { type: 'turn.desynced' }>['payload'] & {
  tieBreakerPlayerId: string | null
}

/**
 * The dedupe key of a desync announcement: the turn and everything a client acts on, so a verdict
 * announced again with the same content is logged once. The reports themselves are left out,
 * because a report that changes without changing the candidates or the tie-breaker asks nothing
 * new of anybody. Server-side only; it never reaches a client.
 */
export const desyncAnnouncementKey = (payload: DesyncPayload): string =>
  `turn.desynced:${payload.turn}:${payload.candidateStateHashes.join(',')}:${payload.tieBreakerPlayerId ?? ''}`

/**
 * Announce a desync verdict again when it differs from what clients were last told about the
 * turn. A verdict can return to an earlier one (a seat that left and rejoined restores the tie
 * its departure broke), and a key naming only the content would find that earlier announcement
 * in the log and drop the new one, leaving every client acting on the departure's verdict while
 * the server enforces the tie: the designated player never learned it was named, and everybody
 * else was refused with `not_tie_breaker`. So the verdict is compared with the latest
 * announcement and, like a status change, keyed by the event it follows.
 *
 * When the latest announcement is of another turn (two turns desynced at once), the turn's own
 * last word is not at hand. A loud call then announces anyway, keyed by that event, because a
 * repeat of an unchanged verdict costs a duplicate clients already tolerate and a dropped change
 * costs the match. A quiet call (the sweep) publishes only a change it can see, so a paused
 * match costs one indexed read per desynced turn and no writes while nothing changes.
 */
export async function reannounceDesync(
  { storage, publisher }: { storage: KernelDeps['storage']; publisher: EventPublisher },
  matchId: string,
  payload: DesyncPayload,
  loud: boolean,
): Promise<void> {
  const last = await storage.events.latestOfType(matchId, 'turn.desynced')
  const lastPayload = last?.type === 'turn.desynced' ? last.payload : null
  if (!last || !lastPayload) {
    if (loud)
      await publisher.publishOnce(matchId, desyncAnnouncementKey(payload), {
        type: 'turn.desynced',
        payload,
      })
    return
  }
  if (lastPayload.turn === payload.turn) {
    if (
      (lastPayload.tieBreakerPlayerId ?? null) === payload.tieBreakerPlayerId &&
      lastPayload.candidateStateHashes.join(',') === payload.candidateStateHashes.join(',')
    ) {
      return
    }
  } else if (!loud) {
    return
  }
  await publisher.publishOnce(matchId, `${desyncAnnouncementKey(payload)}:after:${last.seq}`, {
    type: 'turn.desynced',
    payload,
  })
}
