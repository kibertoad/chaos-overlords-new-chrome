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
     * Reserves one unit of this caller's daily attached-journal budget. Returns a release function
     * when allowed, or null when spent. The handler releases it unless the journal is stored.
     */
    bugReportJournalBudget?: () => (() => void) | null
    /**
     * Spends one unit of the process-wide match-creation budget, or throws the 429 when it is gone.
     * Set by `matchCreationRateLimited`, called by the create handler once the body has validated;
     * absent everywhere else.
     */
    spendMatchCreation?: () => void
    /**
     * The object a contract handler answered with, recorded by `contractJson` so the response
     * validator can check it without parsing the body back out. Wrapped so that a handler
     * answering `null` is still told apart from a handler that recorded nothing.
     */
    responseBody?: { value: unknown }
  }
}
