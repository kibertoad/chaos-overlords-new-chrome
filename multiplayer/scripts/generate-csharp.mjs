#!/usr/bin/env node
import { spawnSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * Regenerates the C# mirror of the wire contracts.
 *
 * The game is a .NET client of a TypeScript server, so one of the two sides has to be derived from
 * the other. This is that derivation: `@game-infra/valibot-to-csharp` walks the valibot source and
 * emits records that deserialize the same JSON. The result is committed, so building the game never
 * needs Node and CI's .NET job never needs this script.
 *
 * ```sh
 * pnpm codegen        # rewrite src/Rechaos.Multiplayer/Generated/WireContracts.cs
 * pnpm codegen:check  # fail if the committed file no longer matches the schemas
 * ```
 *
 * The generator is a pinned devDependency of this workspace rather than something fetched when the
 * script runs. `codegen:check` is a CI step, and CI verifying committed files must not depend on the
 * registry being reachable: a fetch on every run turns an npm hiccup into a red build on whatever
 * unrelated pull request happened to be open. Being in the lockfile also means the version that
 * checks the output is provably the version that wrote it.
 *
 * Pass `--generator` to run an unpublished one out of a sibling `game-infra` checkout:
 *
 * ```sh
 * pnpm codegen --generator "node_modules/.bin/tsx ../game-infra/packages/valibot-to-csharp/src/cli.ts"
 * ```
 */

const here = dirname(fileURLToPath(import.meta.url))
const multiplayerRoot = resolve(here, '..')
const repoRoot = resolve(multiplayerRoot, '..')

const GENERATED_DIR = join(repoRoot, 'src', 'Rechaos.Multiplayer', 'Generated')
const OUTPUT_PATH = join(GENERATED_DIR, 'WireContracts.cs')
const OUTPUT_NAME = 'WireContracts.cs'
const ROUTES_PATH = join(GENERATED_DIR, 'RouteTemplates.cs')
const NAMESPACE = 'Rechaos.Multiplayer.Generated'

/**
 * The generator, pinned exactly in `package.json` rather than to a range.
 *
 * The output is committed and this script also checks it, so the version that wrote the file has to
 * be the version that reads it: under a range, a generator release would turn CI red on whatever
 * unrelated pull request happened to be open that day. Adopting a new one is a deliberate commit —
 * bump the dependency, run `pnpm codegen`, and the diff says what changed.
 *
 * 0.2.0 is the first release the output of this workspace compiles under: `strictObject`, non-string
 * literals, integer bounds, spread field groups, the nullable context and a round-trippable
 * discriminated union all landed in it.
 */
const GENERATOR_SPEC = '@game-infra/valibot-to-csharp'
const DEFAULT_CLI = localBin('valibot-to-csharp')

/**
 * A workspace binary, by absolute path.
 *
 * Resolved rather than shelled through `npx`, which would reach for the registry when the local copy
 * is missing — quietly turning a stale install into a download, and a check of committed files into
 * something that can fail offline.
 */
function localBin(name) {
  return join(multiplayerRoot, 'node_modules', '.bin', name)
}

/**
 * The schema files, not the barrel.
 *
 * The generator follows relative imports itself, so naming `views.ts` pulls in the primitives and
 * settings it depends on. `contracts.ts` is deliberately absent: an endpoint definition is a
 * TypeScript value with a `pathResolver` function in it, and a record cannot stand for that. The
 * C# client spells its routes out instead, and `RouteTemplates` in the .NET tests pins them
 * against these same contracts.
 */
const INPUTS = ['errors.ts', 'events.ts', 'orders.ts', 'schemas.ts', 'views.ts'].map((file) =>
  join(multiplayerRoot, 'packages', 'contracts', 'src', file),
)

async function main() {
  const check = process.argv.includes('--check')
  const scratch = mkdtempSync(join(tmpdir(), 'chaos-codegen-'))
  try {
    const outputs = [
      [OUTPUT_PATH, withGuardBanner(run(scratch))],
      [ROUTES_PATH, routeTemplates()],
    ]
    const stale = outputs.filter(
      ([path, content]) => normalise(readOrEmpty(path)) !== normalise(content),
    )
    if (!check) {
      mkdirSync(GENERATED_DIR, { recursive: true })
      for (const [path, content] of outputs) writeFileSync(path, content, 'utf8')
      process.stdout.write(`wrote ${outputs.map(([path]) => basename(path)).join(', ')}\n`)
      return
    }
    if (stale.length === 0) {
      process.stdout.write('the generated C# is up to date\n')
      return
    }
    process.stderr.write(
      `${stale.map(([path]) => path).join('\n')}\n` +
        'no longer matches the contracts. Run `pnpm codegen` in multiplayer/ and commit the result.\n',
    )
    process.exitCode = 1
  } finally {
    rmSync(scratch, { recursive: true, force: true })
  }
}

/**
 * The route each contract mounts, as a C# constant.
 *
 * An endpoint definition is a TypeScript value with a resolver function in it, so the schema
 * generator has nothing to make a record of. The paths still have to reach the C# client, and a
 * second hand-kept list of them is the thing this whole exercise exists to avoid — so they are read
 * out of the built contracts with the same `mapApiContractToPath` the server derives its routes
 * from, and `ApiRouteTests` holds `ApiRoutes` to them.
 */
function routeTemplates() {
  const entries = readRoutes()
  return [
    '// <auto-generated>',
    '//   This file was generated from multiplayer/packages/contracts/src/contracts.ts.',
    '//   Do not edit by hand.',
    '// Regenerate: cd multiplayer && pnpm codegen',
    '// </auto-generated>',
    '',
    '#nullable enable',
    '',
    'using System.Collections.Generic;',
    '',
    `namespace ${NAMESPACE};`,
    '',
    '/// <summary>Every route the server mounts, as "METHOD /pattern".</summary>',
    'public static class RouteTemplates',
    '{',
    ...entries.map(
      ([name, template]) =>
        `    /// <summary><c>${template}</c></summary>\n    public const string ${name} = ${JSON.stringify(template)};`,
    ),
    '',
    '    /// <summary>All of them, for a test that asserts the client covers the surface.</summary>',
    '    public static IReadOnlyList<string> All { get; } =',
    '    [',
    ...entries.map(([name]) => `        ${name},`),
    '    ];',
    '}',
    '',
  ].join('\n')
}

/** Runs the generator into a scratch directory and returns what it wrote. */
function run(scratch) {
  // Split on whitespace so `--generator "tsx path/to/cli.ts"` works as one argument. The default is
  // a single resolved path, which has nothing to split.
  const cli = (generatorOverride() ?? DEFAULT_CLI).split(/\s+/).filter(Boolean)
  const [command, ...leading] = cli
  const args = [
    ...leading,
    ...INPUTS.flatMap((input) => ['--input', input]),
    '--output',
    scratch,
    '--namespace',
    NAMESPACE,
    '--bundle',
    OUTPUT_NAME,
  ]
  const result = spawnSync(command, args, { cwd: multiplayerRoot, encoding: 'utf8' })
  if (result.error || result.status !== 0) {
    throw new Error(
      `${command} failed (${result.error?.message ?? `exit ${result.status}`}).\n` +
        `${result.stderr ?? ''}\n` +
        `This needs ${GENERATOR_SPEC} installed: run \`pnpm install\` in multiplayer/, or pass ` +
        '--generator pointing at a checkout.',
    )
  }
  return readFileSync(join(scratch, OUTPUT_NAME), 'utf8')
}

/**
 * `[name, "METHOD /pattern"]` for every contract, read out of the TypeScript source.
 *
 * It runs under `tsx` rather than importing the built package: the workspace compiles with
 * bundler module resolution, so its emitted JavaScript carries extensionless relative specifiers
 * that Node's ESM loader will not resolve. Reading the source sidesteps that and needs no build.
 */
function readRoutes() {
  const contractsModule = JSON.stringify(
    join(multiplayerRoot, 'packages', 'contracts', 'src', 'contracts.ts'),
  )
  const script = [
    "import { mapApiContractToPath } from '@toad-contracts/core'",
    `import { API_CONTRACTS } from ${contractsModule}`,
    'const rows = Object.entries(API_CONTRACTS).map(([name, contract]) => [',
    '  name[0].toUpperCase() + name.slice(1),',
    "  contract.method.toUpperCase() + ' ' + mapApiContractToPath(contract),",
    '])',
    'process.stdout.write(JSON.stringify(rows))',
  ].join('\n')
  const result = spawnSync(localBin('tsx'), ['--eval', script], {
    cwd: join(multiplayerRoot, 'packages', 'contracts'),
    encoding: 'utf8',
  })
  if (result.error || result.status !== 0) {
    throw new Error(
      `could not read the endpoint contracts (${result.error?.message ?? `exit ${result.status}`}).\n${result.stderr ?? ''}`,
    )
  }
  return JSON.parse(result.stdout)
}

/** The `--generator "<command>"` argument, when one was passed. */
function generatorOverride() {
  const index = process.argv.indexOf('--generator')
  return index >= 0 ? process.argv[index + 1] : undefined
}

/** Line-ending-agnostic, so a checkout with CRLF does not read as drift. */
function normalise(text) {
  return `${text.replace(/\r\n/g, '\n').trimEnd()}\n`
}

function readOrEmpty(path) {
  try {
    return readFileSync(path, 'utf8')
  } catch {
    return ''
  }
}

/**
 * The generator's own banner says not to edit the file. This adds where it came from and how to
 * regenerate it, which is what somebody who has just changed a schema actually needs.
 */
function withGuardBanner(source) {
  const banner = [
    '// Source: multiplayer/packages/contracts/src/*.ts (valibot).',
    '// Regenerate: cd multiplayer && pnpm codegen',
    '',
  ].join('\n')
  const marker = '// </auto-generated>\n'
  const index = source.indexOf(marker)
  if (index < 0) return `${banner}${source}`
  const cut = index + marker.length
  return `${source.slice(0, cut)}${banner}${source.slice(cut)}`
}

await main()
