#!/usr/bin/env node
// Packs every publishable package into one directory, in dependency order.
//
//   node scripts/pack.mjs <destination>
//
// The release workflow packs in a job that holds no OIDC identity and publishes the resulting
// tarballs from a second job that holds nothing else. That split is the point: the packing job runs
// `pnpm install`, the whole build, the whole test suite and the codegen check, and any of those —
// an install script, a vitest plugin, a transitive dev dependency — could otherwise ask GitHub for
// the job's id-token and trade it at the registry for permission to publish all nine packages with
// valid provenance pointing at this repository.
//
// It also makes the published bytes the rehearsed bytes. Publishing from the workspace re-ran each
// package's `prepublishOnly`, which rebuilds, so what reached the registry was never quite the
// artifact the release had just tested.

import { execFileSync } from 'node:child_process'
import { mkdirSync, readdirSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { run } from './lib/cli.mjs'
import { readPublishableManifests } from './lib/versions.mjs'

const workspaceRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')

run(() => {
  const destination = process.argv[2]
  if (!destination) throw new Error('Usage: node scripts/pack.mjs <destination>')
  const outputDirectory = resolve(destination)
  mkdirSync(outputDirectory, { recursive: true })

  const manifests = readPublishableManifests()
  for (const manifest of manifests) {
    const packageDirectory = join(workspaceRoot, dirname(manifest.path))
    execFileSync(
      process.platform === 'win32' ? 'pnpm.cmd' : 'pnpm',
      ['pack', '--pack-destination', outputDirectory],
      { cwd: packageDirectory, stdio: 'inherit' },
    )
  }

  const packed = readdirSync(outputDirectory).filter((name) => name.endsWith('.tgz'))
  if (packed.length !== manifests.length) {
    throw new Error(
      `Expected ${manifests.length} tarballs in ${outputDirectory}, found ${packed.length}.`,
    )
  }
  console.log(`Packed ${packed.length} tarballs into ${outputDirectory}.`)
})
