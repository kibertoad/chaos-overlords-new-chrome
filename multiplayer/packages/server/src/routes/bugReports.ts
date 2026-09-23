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
    const release = request.state === undefined ? undefined : c.get('bugReportJournalBudget')?.()
    const denied = release === null
    const submitted = denied ? { ...request, state: undefined } : request
    let stored = false
    try {
      const receipt = await bugReports.submit(submitted)
      stored = receipt.stateStored === 'stored'
      // The intake sees no journal when this address is spent, but the client did send one.
      return c.json(answering(c, denied ? { ...receipt, stateStored: 'omitted' } : receipt), 201)
    } finally {
      // A digest refusal, global-byte omission, row failure, or any other non-storage outcome
      // cannot spend the caller's allowance. The reservation is per request and safe to release
      // even if another request has since entered the same address window.
      if (!stored) release?.()
    }
  })
}
