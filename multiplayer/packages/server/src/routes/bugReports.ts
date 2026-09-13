import { submitBugReportContract } from '@chaos-overlords/contracts'
import { NotFoundError } from '@chaos-overlords/kernel'
import type { Hono } from 'hono'
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
    return c.json(await bugReports.submit(c.req.valid('json')), 201)
  })
}
