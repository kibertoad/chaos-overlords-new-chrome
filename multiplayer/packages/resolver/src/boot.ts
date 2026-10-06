/**
 * Boots the WebAssembly build of the game's rules from the modules `scripts/bundle.mjs` lays out.
 *
 * The same function runs in a Node worker thread and in a workerd isolate, so the two hosts start
 * the runtime the same way. Only how the modules and the assembly bytes arrive differs: a Worker
 * imports them at upload, Node reads the files.
 */

/** `bundle/manifest.json`. */
export interface BundleManifest {
  /** The session version this build plays: `MultiplayerSessionVersion.Current` when it was built. */
  sessionVersion: number
  /** The native save format of the payloads `SavePayload` writes and `Restore` reads. */
  snapshotFormatVersion: number
  mainAssemblyName: string
  /** The loader's file names for the two runtime modules and the WebAssembly runtime. */
  runtime: { native: string; runtime: string; wasm: string }
  /** Every assembly, by the name the loader asks for and the file it is in. */
  assemblies: { name: string; file: string }[]
}

/** `Rechaos.Resolver.Wasm.ResolverExports`, as the .NET interop layer exposes it. */
export interface ResolverExports {
  SessionVersion(): number
  SnapshotFormatVersion(): number
  Bootstrap(seed: number, gameSettingsJson: string, playersJson: string): number
  Restore(savePayload: Uint8Array, stateHash: string): number
  ApplySealedTurn(handle: number, sealedOrdersJson: string): string
  HandOverSeat(handle: number, slot: number, toComputer: boolean): void
  StateHash(handle: number): string
  Turn(handle: number): number
  IsFinished(handle: number): boolean
  SavePayload(handle: number): Uint8Array
  ManagedHeapBytes(collect: boolean): number
  Release(handle: number): void
}

/** What a host hands {@link bootResolver}. */
export interface ResolverModules {
  /** The `dotnet` builder `bundle/dotnet.js` exports. */
  dotnet: unknown
  /** The namespaces of `bundle/dotnet.native.js` and `bundle/dotnet.runtime.js`. */
  nativeModule: unknown
  runtimeModule: unknown
  /** `bundle/dotnet.native.wasm`, compiled. */
  wasm: WebAssembly.Module
  manifest: BundleManifest
  /** The bytes of one assembly, by the name the loader asks for. */
  assembly(name: string): ArrayBuffer | Uint8Array | undefined
}

/** A started runtime. */
export interface BootedResolver {
  exports: ResolverExports
  manifest: BundleManifest
  /** The size of the WebAssembly memory, which only grows. */
  memoryBytes(): number
}

interface DotnetBuilder {
  withConfig(config: unknown): DotnetBuilder
  withRuntimeOptions(options: string[]): DotnetBuilder
  withResourceLoader(
    loader: (type: string, name: string) => Promise<Response> | undefined,
  ): DotnetBuilder
  create(): Promise<DotnetRuntime>
}

interface DotnetRuntime {
  getAssemblyExports(name: string): Promise<Record<string, unknown>>
  Module: { HEAPU8: Uint8Array }
}

/**
 * The interpreter's trace compiler (the "jiterpreter") compiles small WebAssembly modules at run
 * time, which workerd refuses ("Wasm code generation disallowed by embedder"). It is off in both
 * hosts, so they run the same code.
 */
const RUNTIME_OPTIONS = [
  '--no-jiterpreter-traces-enabled',
  '--no-jiterpreter-interp-entry-enabled',
  '--no-jiterpreter-jit-call-enabled',
]

export async function bootResolver(modules: ResolverModules): Promise<BootedResolver> {
  const { manifest } = modules
  // workerd compiles WebAssembly only at upload. The loader compiles the runtime itself, so its one
  // compile is answered with the module the host already has, for as long as the boot takes.
  const original = { compile: WebAssembly.compile, compileStreaming: WebAssembly.compileStreaming }
  const precompiled = async () => modules.wasm
  WebAssembly.compile = precompiled
  WebAssembly.compileStreaming = precompiled
  let runtime: DotnetRuntime
  try {
    runtime = await (modules.dotnet as DotnetBuilder)
      .withConfig({
        resources: {
          jsModuleNative: [{ name: manifest.runtime.native, moduleExports: modules.nativeModule }],
          jsModuleRuntime: [
            { name: manifest.runtime.runtime, moduleExports: modules.runtimeModule },
          ],
        },
      })
      .withRuntimeOptions(RUNTIME_OPTIONS)
      .withResourceLoader((type, name) => {
        if (type === 'dotnetwasm') {
          return Promise.resolve(
            new Response(new Uint8Array(0), { headers: { 'content-type': 'application/wasm' } }),
          )
        }
        const bytes = modules.assembly(name)
        return bytes ? Promise.resolve(new Response(bytes as BodyInit)) : undefined
      })
      .create()
  } finally {
    WebAssembly.compile = original.compile
    WebAssembly.compileStreaming = original.compileStreaming
  }
  const assemblyExports = await runtime.getAssemblyExports(manifest.mainAssemblyName)
  const exports = (
    assemblyExports as { Rechaos: { Resolver: { Wasm: { ResolverExports: ResolverExports } } } }
  ).Rechaos.Resolver.Wasm.ResolverExports
  const playing = exports.SessionVersion()
  if (playing !== manifest.sessionVersion) {
    throw new Error(
      `the resolver bundle says it plays session version ${manifest.sessionVersion} but the build plays ${playing}`,
    )
  }
  return { exports, manifest, memoryBytes: () => runtime.Module.HEAPU8.byteLength }
}
