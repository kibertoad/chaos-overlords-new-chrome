import type { Principal } from '@chaos-overlords/kernel'
import type { ApiContract } from '@toad-contracts/core'
import type { ServerContainer } from '../container'

export interface AppEnv {
  Variables: {
    container: ServerContainer
    requestId: string
    principal: Principal
    /** Set by the contract route before the handler runs; absent on non-contract routes. */
    apiContract?: ApiContract
    /**
     * Whether this caller's address still has room in its daily attached-journal budget. Set by
     * `bugReportRateLimited`; absent everywhere else.
     */
    bugReportStateAllowed?: boolean
  }
}
