import { submitBugReportContract } from '@chaos-overlords/contracts'
import { NotFoundError } from '@chaos-overlords/kernel'
import type { Hono } from 'hono'
import { answering } from '../http/contractJson'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'

/**
 * The bug report door.
 *
 * Unauthenticated, and registered alongside the two lobby doors that are. A deployment that has not
 * configured the intake answers 404 rather than 500: "this server does not take bug reports" is a
 * true and actionable thing to tell a client, and it is the same shape the public lobby listing
 * uses when it is switched off.
 */
export function registerBugReportRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, submitBugReportContract, async (c) => {
    const { bugReports } = c.get('container')
    if (!bugReports) {
      throw new NotFoundError('This server does not accept bug reports', {
        reason: 'bug_reports_disabled',
      })
    }
    const request = c.req.valid('json')
    // The daily journal budget is spent HERE: after the contract has validated the body, and only
    // when there is a journal to spend it on. Charging it in the middleware meant five text-only
    // reports, or five bodies the validator refused, used up the day's allowance for everyone
    // behind one address — and the sixth report, the one carrying the journal somebody wanted, was
    // filed without it and said so only in the receipt.
    //
    // An address that really has spent it files the description without the journal, and the
    // receipt says `omitted` — the same outcome the global byte budget produces.
    const allowed = request.state === undefined || (c.get('bugReportJournalBudget')?.() ?? true)
    const submitted = allowed ? request : { ...request, state: undefined }
    return c.json(answering(c, await bugReports.submit(submitted)), 201)
  })
}
