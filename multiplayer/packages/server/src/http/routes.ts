import { honoContractRoutes } from '@toad-contracts/hono'
import type { AppEnv } from './types'

/**
 * Route registration bound to this app's environment, so a contract handler's `c.get('container')`
 * resolves alongside `c.req.valid(...)`.
 *
 * Every route is mounted from a contract in `@chaos-overlords/contracts`: the method, the path and
 * the validation of each request target come from the one definition the TypeScript client calls
 * through and the C# client's records are generated from. Nothing here restates a path.
 */
export const { buildHonoRoute } = honoContractRoutes<AppEnv>()
