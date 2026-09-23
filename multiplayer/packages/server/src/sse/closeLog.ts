import type { Logger } from '@chaos-overlords/kernel'
import { isUnexpectedClose } from './createSseResponse'
import type { EventHubObserver } from './LocalEventHub'

/**
 * The `closed` observer every runtime wants: one line per ended stream, loud when the server
 * dropped a stream its client was still reading.
 *
 * A client going away is the ordinary end of every stream, so it stays at debug; a stream the
 * server ended for a reason of its own is the one an operator is looking for when a player
 * reports a lost connection on a server that was up.
 */
export function logStreamClosed(logger: Logger): NonNullable<EventHubObserver['closed']> {
  return (matchId, playerId, reason, openMs) => {
    const fields = { matchId, playerId, reason, openMs }
    if (isUnexpectedClose(reason)) logger.warn('event stream dropped by the server', fields)
    else if (reason === 'client_gone') logger.debug('event stream closed by the client', fields)
    else logger.info('event stream closed', fields)
  }
}
