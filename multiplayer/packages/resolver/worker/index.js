// The resolver Worker: deploy this module as a Worker of its own, without the `nodejs_compat` flag,
// and bind it to the coordination Worker as a service. `wrangler.toml` beside it lists what a
// deployment needs, and the package README explains the setup.
//
// A Worker imports nothing at run time and compiles no WebAssembly after upload, so every module of
// the bundle is imported here, statically: the runtime as a compiled module, every assembly as Data.
import { dotnet } from '../bundle/dotnet.js'
import * as nativeModule from '../bundle/dotnet.native.js'
import * as runtimeModule from '../bundle/dotnet.runtime.js'
import wasm from '../bundle/dotnet.native.wasm'
import { assemblies } from '../bundle/assemblies.js'
import manifest from '../bundle/manifest.js'
import {
  MatchResolverObject,
  ResolverEntrypoint,
  useResolverBundle,
} from '../dist/workerd/index.js'

useResolverBundle({ dotnet, nativeModule, runtimeModule, wasm, manifest, assemblies })

export { MatchResolverObject }
export default ResolverEntrypoint
