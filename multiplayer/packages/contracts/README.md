# @chaos-overlords/contracts

Wire contracts for the [*Chaos Overlords: New Chrome*](https://github.com/kibertoad/chaos-overlords-new-chrome) multiplayer server.

Valibot schemas for every multiplayer request, view and event, the error envelope, and one
`defineApiContract` per endpoint. The server mounts its routes from these and the clients decode
against them, so neither side types the wire format a second time.

```ts
import { createMatchContract, matchViewSchema } from '@chaos-overlords/contracts'
```

## Install

```sh
npm install @chaos-overlords/contracts
```

Requires Node.js 22 or newer.

## Documentation

The operator and contributor manual for the whole multiplayer workspace is in
[`multiplayer/README.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/multiplayer/README.md); the design notes are in
[`docs/MULTIPLAYER.md`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/docs/MULTIPLAYER.md).

## Licence

GPL-3.0-only. See [`LICENSE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/LICENSE) and [`NOTICE`](https://github.com/kibertoad/chaos-overlords-new-chrome/blob/main/NOTICE).
