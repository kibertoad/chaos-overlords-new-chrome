import {
  createMatchContract,
  getMatchContract,
  joinMatchContract,
  joinRunningMatchContract,
  kickPlayerContract,
  leaveMatchContract,
  listLobbiesContract,
  rejoinMatchContract,
  startMatchContract,
  takeoverVoteContract,
  updateMatchSettingsContract,
  updatePlayerProfileContract,
} from '@chaos-overlords/contracts'
import { NotFoundError } from '@chaos-overlords/kernel'
import type { Hono } from 'hono'
import { answering } from '../http/contractJson'
import { requireMember } from '../http/guards'
import { buildHonoRoute } from '../http/routes'
import type { AppEnv } from '../http/types'

/**
 * Browse, create and join: the three routes that answer without a token.
 *
 * They are registered before the app's `/matches/:matchId/*` auth mount, whose pattern also matches
 * `/matches/join`. Hono ends the chain at the first matching handler, so being registered first is
 * what keeps joining a lobby from demanding the token a player is joining to get.
 */
export function registerPublicLobbyRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, listLobbiesContract, async (c) => {
    const { config, kernel } = c.get('container')
    if (!config.publicListing) {
      throw new NotFoundError('This server does not list public matches', {
        reason: 'listing_disabled',
      })
    }
    const { sessionVersion } = c.req.valid('query')
    return c.json(
      answering(c, { matches: await kernel.query.listPublicLobbies(50, sessionVersion) }),
      200,
    )
  })

  buildHonoRoute(api, createMatchContract, async (c) => {
    // Charged here, after the contract validator, so a body that was never going to store a lobby
    // does not spend the budget every host shares; `matchCreationRateLimited` only checks it.
    c.get('spendMatchCreation')?.()
    const membership = await c.get('container').kernel.lobby.createMatch(c.req.valid('json'))
    return c.json(answering(c, membership), 201)
  })

  // Both join doors verify a password, so both hand the kernel the caller the attempt belongs to:
  // its per-caller budget in front of PBKDF2 is what keeps one stranger from spending a match's.
  // `rateLimited` put the key there; see `AppEnv`.
  buildHonoRoute(api, joinMatchContract, async (c) => {
    const membership = await c
      .get('container')
      .kernel.lobby.join(c.req.valid('json'), c.get('caller'))
    return c.json(answering(c, membership), 201)
  })

  buildHonoRoute(api, joinRunningMatchContract, async (c) => {
    const membership = await c
      .get('container')
      .kernel.lobby.joinRunning(c.req.valid('json'), c.get('caller'))
    return c.json(answering(c, membership), 201)
  })
}

/** The rest of the lobby lifecycle, for a seated member. */
export function registerMemberLobbyRoutes(api: Hono<AppEnv>): void {
  buildHonoRoute(api, getMatchContract, async (c) => {
    const principal = requireMember(c.get('principal'), c.req.valid('param').matchId)
    const { kernel } = c.get('container')
    // A lobby poll that has seen this view already is answered 304 from the tag alone; see
    // `MatchQueryService.lobbyTag`. Taken before the view is read, so the tag is never newer than
    // the view it goes out with.
    const tag = await kernel.query.lobbyTag(
      principal.match,
      principal.player.id,
      kernel.deps.clock.now(),
    )
    // Set only on the answers that carry the view or stand for it. Hono copies headers already set
    // on the context onto an error response, so setting them before the view read would tag a
    // refusal thrown by that read.
    const tagAnswer = (current: string) => {
      // Per member and current only for a moment: no shared cache may keep it, and a private one
      // must ask again every time.
      c.header('Cache-Control', 'private, no-cache')
      c.header('ETag', current)
    }
    if (tag !== null && matchesIfNoneMatch(c.req.header('if-none-match'), tag)) {
      tagAnswer(tag)
      return c.body(null, 304)
    }
    // A client reads the view to resynchronise, and one that is doing so because its countdown ran
    // out with no seal is owed the seal rather than the same stuck turn; see `sealIfOverdue`.
    const match = await kernel.turns.sealIfOverdue(principal.match)
    const view = await kernel.query.view(match)
    if (tag !== null) tagAnswer(tag)
    return c.json(
      answering(c, {
        match: view,
        joinCode: principal.match.joinCode,
        you: principal.player.id,
      }),
      200,
    )
  })

  buildHonoRoute(api, startMatchContract, async (c) => {
    await c
      .get('container')
      .kernel.lobby.start(requireMember(c.get('principal'), c.req.valid('param').matchId))
    return c.body(null, 204)
  })

  buildHonoRoute(api, updateMatchSettingsContract, async (c) => {
    await c
      .get('container')
      .kernel.lobby.updateSettings(
        requireMember(c.get('principal'), c.req.valid('param').matchId),
        c.req.valid('json'),
      )
    return c.body(null, 204)
  })

  buildHonoRoute(api, updatePlayerProfileContract, async (c) => {
    await c
      .get('container')
      .kernel.lobby.updateProfile(
        requireMember(c.get('principal'), c.req.valid('param').matchId),
        c.req.valid('json'),
      )
    return c.body(null, 204)
  })

  buildHonoRoute(api, leaveMatchContract, async (c) => {
    await c
      .get('container')
      .kernel.lobby.leave(requireMember(c.get('principal'), c.req.valid('param').matchId))
    return c.body(null, 204)
  })

  buildHonoRoute(api, rejoinMatchContract, async (c) => {
    await c
      .get('container')
      .kernel.lobby.rejoin(requireMember(c.get('principal'), c.req.valid('param').matchId))
    return c.body(null, 204)
  })

  buildHonoRoute(api, kickPlayerContract, async (c) => {
    const { matchId, playerId } = c.req.valid('param')
    await c.get('container').kernel.lobby.kick(requireMember(c.get('principal'), matchId), playerId)
    return c.body(null, 204)
  })

  buildHonoRoute(api, takeoverVoteContract, async (c) => {
    const { matchId, playerId } = c.req.valid('param')
    await c
      .get('container')
      .kernel.lobby.voteOnTakeover(
        requireMember(c.get('principal'), matchId),
        playerId,
        c.req.valid('json'),
      )
    return c.body(null, 204)
  })
}

/**
 * Whether an `If-None-Match` list names `tag`, by the weak comparison RFC 9110 prescribes for it.
 *
 * The list is comma-separated, a `W/` prefix is ignored on either side, and `*` matches any current
 * representation, which a lobby always has.
 */
export function matchesIfNoneMatch(header: string | undefined, tag: string): boolean {
  if (header === undefined) return false
  const opaque = (value: string) => value.trim().replace(/^W\//, '')
  const wanted = opaque(tag)
  return header.split(',').some((candidate) => {
    const value = opaque(candidate)
    return value === '*' || value === wanted
  })
}
