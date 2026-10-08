// The coordination Worker's side of the Cloudflare host, as the workerd tests run it: a Worker with
// `nodejs_compat`, the resolver Worker bound as RESOLVER, and the package's client over it. Each
// POST is one MatchResolver call, so a test under Node drives the whole path, archive codec and
// RPC included, through fetch.
import { cloudflareMatchResolver } from '../../dist/cloudflare/index.js'
import { resolverErrorCode } from '../../dist/errors.js'

export default {
  async fetch(request, env) {
    const { method, args } = await request.json()
    const resolver = cloudflareMatchResolver(env.RESOLVER)
    try {
      const value =
        method === 'info' ? await env.RESOLVER.info(...args) : await resolver[method](...args)
      return Response.json({ ok: true, value: value ?? null })
    } catch (error) {
      return Response.json({
        ok: false,
        name: error?.name,
        code: resolverErrorCode(error) ?? null,
        message: String(error?.message ?? error),
      })
    }
  },
}
