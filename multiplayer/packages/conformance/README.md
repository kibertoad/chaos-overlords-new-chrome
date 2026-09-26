# @chaos-overlords/conformance

Conformance suites for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

Runtime-neutral behaviour suites: every storage implementation and every runtime facade runs
the same assertions, so "it works on Node" and "it works on Workers" mean the same thing.

Published for anyone hosting an alternative storage or runtime. `vitest` is a peer dependency.

## Install

```sh
npm install @chaos-overlords/conformance
```

Requires Node.js 22 or newer.

## Documentation

The operator and contributor manual for the whole multiplayer workspace is in
[`multiplayer/README.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md); the design notes are in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md).

## Licence

MIT. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
