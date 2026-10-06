#!/usr/bin/env node
// Publishes src/Rechaos.Resolver.Wasm and lays it out in bundle/, the form both hosts load:
//
//   node scripts/bundle.mjs [--framework <_framework dir>]
//
// With no argument it runs `dotnet publish` (the .NET SDK the repository's global.json names; set
// DOTNET to use another executable). `--framework` lays out a publish that already exists.
//
// One layout serves Node and workerd, so the boot code both run is the same. workerd has no file
// system and no dynamic import of a URL, and it compiles WebAssembly only at upload, so:
//
// - the runtime's two JavaScript modules (`dotnet.native.js`, `dotnet.runtime.js`) are imported
//   statically and handed to the loader as preloaded modules, and the boot config embedded in
//   `dotnet.js` loses its entries for them: the loader adds the caller's module entries to the
//   embedded ones rather than replacing them, and would try to fetch the embedded ones;
// - `import.meta.url`, which workerd leaves undefined, falls back to a fixed base in the loader's
//   three files. The loader only resolves resource names against it and every resource is answered
//   from memory, so nothing is fetched from it. Node keeps its own, which the loader needs there for
//   `createRequire`;
// - `dotnet.native.wasm` is a module of its own, which a Worker imports as a compiled module and
//   Node compiles once;
// - every assembly is `assemblies/<n>.bin`, which a Worker imports as a Data module, and
//   `assemblies.js` imports them all statically for the Worker, while `manifest.json` names them for
//   Node, which reads the files (`manifest.js` is the same for the Worker, which cannot import JSON).
//
// manifest.json also records the session version the build plays and the native save format of the
// snapshots it writes, read from the C# source the publish compiled. The hosts check the first
// against the build's own SessionVersion() export when they boot it.

import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const packageDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const repositoryRoot = path.resolve(packageDir, '..', '..', '..')
const project = path.join(
  repositoryRoot,
  'src',
  'Rechaos.Resolver.Wasm',
  'Rechaos.Resolver.Wasm.csproj',
)
const outDir = path.join(packageDir, 'bundle')
const BASE = 'https://resolver.invalid/_framework/'

function frameworkDir() {
  const index = process.argv.indexOf('--framework')
  if (index >= 0) {
    const given = process.argv[index + 1]
    if (!given) throw new Error('--framework needs a directory')
    return { dir: path.resolve(given), cleanup: () => {} }
  }
  const publishDir = fs.mkdtempSync(path.join(os.tmpdir(), 'rechaos-resolver-publish-'))
  const dotnet = process.env.DOTNET || 'dotnet'
  execFileSync(dotnet, ['publish', project, '-c', 'Release', '-o', publishDir, '-v', 'minimal'], {
    stdio: 'inherit',
    cwd: repositoryRoot,
  })
  return {
    dir: path.join(publishDir, 'wwwroot', '_framework'),
    cleanup: () => fs.rmSync(publishDir, { recursive: true, force: true }),
  }
}

/** An integer constant from a C# source file, by its declaration. */
function csharpConstant(relativePath, pattern) {
  const source = fs.readFileSync(path.join(repositoryRoot, relativePath), 'utf8')
  const match = pattern.exec(source)
  if (!match) throw new Error(`${relativePath} no longer declares ${pattern}`)
  return Number(match[1])
}

const framework = frameworkDir()
try {
  const loader = fs.readFileSync(path.join(framework.dir, 'dotnet.js'), 'utf8')
  const begin = loader.indexOf('/*json-start*/') + '/*json-start*/'.length
  const end = loader.indexOf('/*json-end*/')
  if (begin < '/*json-start*/'.length || end < begin) {
    throw new Error('dotnet.js carries no embedded boot config')
  }
  const config = JSON.parse(loader.slice(begin, end))
  const resources = config.resources
  const files = {
    native: resources.jsModuleNative[0].name,
    runtime: resources.jsModuleRuntime[0].name,
    wasm: resources.wasmNative[0].name,
  }
  delete resources.jsModuleNative
  delete resources.jsModuleRuntime
  delete resources.pdb
  delete resources.corePdb

  fs.rmSync(outDir, { recursive: true, force: true })
  fs.mkdirSync(path.join(outDir, 'assemblies'), { recursive: true })
  const write = (file, text) =>
    fs.writeFileSync(
      path.join(outDir, file),
      text.replaceAll(
        'import.meta.url',
        `(import.meta.url ?? ${JSON.stringify(`${BASE}${file}`)})`,
      ),
    )
  write('dotnet.js', loader.slice(0, begin) + JSON.stringify(config) + loader.slice(end))
  write('dotnet.native.js', fs.readFileSync(path.join(framework.dir, files.native), 'utf8'))
  write('dotnet.runtime.js', fs.readFileSync(path.join(framework.dir, files.runtime), 'utf8'))
  fs.copyFileSync(path.join(framework.dir, files.wasm), path.join(outDir, 'dotnet.native.wasm'))

  const assemblies = [...(resources.coreAssembly ?? []), ...(resources.assembly ?? [])].map(
    (asset, index) => {
      const file = `assemblies/${index}.bin`
      fs.copyFileSync(path.join(framework.dir, asset.name), path.join(outDir, file))
      return { name: asset.name, file, bytes: fs.statSync(path.join(outDir, file)).size }
    },
  )
  fs.writeFileSync(
    path.join(outDir, 'assemblies.js'),
    [
      '// Generated by scripts/bundle.mjs: every assembly as a module a Worker imports as Data.',
      ...assemblies.map((asset, index) => `import a${index} from './${asset.file}'`),
      '',
      'export const assemblies = {',
      ...assemblies.map((asset, index) => `  ${JSON.stringify(asset.name)}: a${index},`),
      '}',
      '',
    ].join('\n'),
  )

  const manifest = {
    sessionVersion: csharpConstant(
      'src/Rechaos.Multiplayer/Protocol/MultiplayerSessionVersion.cs',
      /public const int Current = (\d+);/,
    ),
    snapshotFormatVersion: csharpConstant(
      'src/Rechaos.Core/Persistence/NativeSaveSerializer.cs',
      /public const int CurrentFormatVersion = (\d+);/,
    ),
    mainAssemblyName: config.mainAssemblyName,
    runtime: files,
    assemblies: assemblies.map(({ name, file }) => ({ name, file })),
  }
  fs.writeFileSync(path.join(outDir, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`)
  // The same for a Worker, which cannot import JSON.
  fs.writeFileSync(
    path.join(outDir, 'manifest.js'),
    `// Generated by scripts/bundle.mjs from manifest.json.\nexport default ${JSON.stringify(manifest)}\n`,
  )

  const assemblyBytes = assemblies.reduce((sum, asset) => sum + asset.bytes, 0)
  const wasmBytes = fs.statSync(path.join(outDir, 'dotnet.native.wasm')).size
  console.log(
    `bundled ${assemblies.length} assemblies (${assemblyBytes} bytes) and a ${wasmBytes}-byte ` +
      `runtime for session version ${manifest.sessionVersion} into ${outDir}`,
  )
} finally {
  framework.cleanup()
}
