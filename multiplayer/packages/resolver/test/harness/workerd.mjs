// Runs the resolver Worker under workerd, through Miniflare, behind a coordination-side Worker
// (caller.js) that reaches it over a service binding, as a deployment does. Used by the package's
// tests and by tools/ResolverDeterminism/check.mjs.
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
// Miniflare 5 takes a new shape of options; the Worker definitions below are in the v4 shape its
// own converter accepts, which is the one its README documents. The converter refuses module rules,
// so every module is listed, main module first, as a deployment's rules would classify it.
import { convertV4MiniflareOptions, Miniflare } from 'miniflare'

const packageDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..')

/** Every file under `dir` (relative to the package) whose name ends with `suffix`. */
function filesUnder(dir, suffix) {
  return fs
    .readdirSync(path.join(packageDir, dir), { recursive: true })
    .map(String)
    .filter((name) => name.endsWith(suffix))
    .map((name) => path.join(packageDir, dir, name))
}

function modules(main, ...rest) {
  const seen = new Set()
  return [main, ...rest].flat().filter((module) => !seen.has(module.path) && seen.add(module.path))
}

const esm = (file) => ({ type: 'ESModule', path: file })

/**
 * @param {{ vars?: Record<string, string> }} [options]
 * @returns {Promise<{ resolver: any, dispose(): Promise<void> }>} `resolver` is a MatchResolver,
 *   plus `info(matchId)`, the memory and holdings of the isolate the match's object runs in.
 */
export async function startWorkerdResolver(options = {}) {
  const mf = new Miniflare(
    convertV4MiniflareOptions({
      // No request.cf: Miniflare would fetch it and cache it in .wrangler/ under the working directory.
      cf: false,
      workers: [
        {
          name: 'caller',
          modulesRoot: packageDir,
          modules: modules(
            esm(path.join(packageDir, 'test', 'harness', 'caller.js')),
            filesUnder('dist', '.js').map(esm),
          ),
          compatibilityDate: '2025-06-01',
          compatibilityFlags: ['nodejs_compat'],
          serviceBindings: { RESOLVER: 'resolver' },
        },
        {
          name: 'resolver',
          modulesRoot: packageDir,
          modules: modules(
            esm(path.join(packageDir, 'worker', 'index.js')),
            filesUnder('dist', '.js').map(esm),
            filesUnder('bundle', '.js').map(esm),
            filesUnder('bundle', '.wasm').map((file) => ({ type: 'CompiledWasm', path: file })),
            filesUnder(path.join('bundle', 'assemblies'), '.bin').map((file) => ({
              type: 'Data',
              path: file,
            })),
          ),
          compatibilityDate: '2025-06-01',
          durableObjects: { MATCH_RESOLVER: { className: 'MatchResolverObject', useSQLite: true } },
          bindings: options.vars ?? {},
        },
      ],
    }),
  )
  await mf.ready
  const call = async (method, args) => {
    const response = await mf.dispatchFetch('http://caller/', {
      method: 'POST',
      body: JSON.stringify({ method, args }),
    })
    const reply = await response.json()
    if (reply.ok) return reply.value
    const error = new Error(reply.message)
    error.name = reply.name
    error.code = reply.code
    throw error
  }
  const resolver = new Proxy(
    {},
    { get: (_, method) => (method === 'then' ? undefined : (...args) => call(method, args)) },
  )
  return { resolver, dispose: () => mf.dispose() }
}
