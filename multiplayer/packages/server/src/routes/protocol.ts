import { handshakeContract, MULTIPLAYER_PROTOCOL_VERSION } from '@chaos-overlords/contracts'
import { DomainError } from '@chaos-overlords/kernel'
import type { Hono } from 'hono'
import { answering } from '../http/contractJson'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'

/** The first call a game makes, before it sends or receives any match data. */
export function registerProtocolRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, handshakeContract, async (c) => {
    const clientVersion = c.req.valid('json').protocolVersion
    const serverVersion = MULTIPLAYER_PROTOCOL_VERSION
    if (clientVersion !== serverVersion) {
      const action =
        clientVersion < serverVersion
          ? 'Update your game to connect to this server.'
          : 'This server is outdated and needs an update.'
      throw new DomainError(
        'conflict',
        `Protocol version mismatch: client version ${clientVersion}, server version ${serverVersion}. ${action}`,
        { reason: 'protocol_version_mismatch', clientVersion, serverVersion },
      )
    }
    return c.json(answering(c, { protocolVersion: serverVersion }), 200)
  })
}
