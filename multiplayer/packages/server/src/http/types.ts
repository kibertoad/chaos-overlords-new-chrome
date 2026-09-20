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
     * Who this request is attributed to, normalised the way the limiters key on. Set by
     * `rateLimited`, read by the handlers that charge a budget of their own below the transport —
     * the join doors, whose password check is PBKDF2 — and absent on every other route.
     */
    caller?: string
    /**
     * Spends one unit of this caller's daily attached-journal budget and says whether there was
     * any left. Set by `bugReportRateLimited`, called by the handler and only for a report that
     * actually carries a journal; absent everywhere else.
     */
    bugReportJournalBudget?: () => boolean
    /**
     * The object a contract handler answered with, recorded by `contractJson` so the response
     * validator can check it without parsing the body back out. Wrapped so that a handler
     * answering `null` is still told apart from a handler that recorded nothing.
     */
    responseBody?: { value: unknown }
  }
}
