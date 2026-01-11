# Rewrite from `main`: Shalimar “server-defined tree → generated TanStack” plan

This document captures the design constraints and the implementation plan so we don’t regress into “route-file bloat” or “tests shaping architecture”.

## Vision (the invariant)

- **Server defines everything**: routes, props trees, data modes, mutations, invalidations, and RouterContext.
- **Generated TanStack**: route tree, loaders, and stores are generated from server-authored contracts.
- **Vertical slice TSX components are pure**:
  - consume typed props + typed RouterContext
  - consume generated stores/selectors/mutations
  - contain UI only (S2 + Tailwind), no route wiring, no fetch strings.
- **Never edit generated files**:
  - `Generated/*.g.ts`, `Generated/routeTree.gen.ts` and any future generated route modules.

## Hard rules (non-negotiable)

- **TanStack Router virtual file routes** only; plugin uses `virtualRouteConfig` and generates `routeTree.gen.ts`.
- **TanStack Store** only for state (`@tanstack/store` + `@tanstack/react-store`).
- **S2-only** component system; shadcn is **inspiration**, not dependency.
- **Server-truth**:
  - client never “blocks” a mutation based on client schema validation.
  - FluentValidation errors returned by server are authoritative.
- **No bloat in routes**:
  - routes and loaders are generated.
  - humans do not author route modules (eventually).

## The correct mental model

It is not “endpoint inventory”. It is a **contract graph**:

- Minimal API endpoints are the **source of truth** for all URLs.
- The “tree” is the **composition of props**, where each non-immediate leaf is bound to a real endpoint.
- The generator validates that **every leaf is satisfied by a real endpoint**, and then generates TanStack code from that graph.

## Tree design (server-authored contract graph)

### Node kinds

1) **Route node (Immediate)**
   - has URL path
   - has a TSX renderer binding
   - returns immediate props which may contain handles

2) **Component node (Immediate, non-route)**
   - has TSX renderer binding
   - no URL path (composed into parent props)

3) **Mode leaf node**
   - `Deferred<T>`, `Lazy<T>`, `Stream<T>`, `Sse<TEvent>`
   - leaf is satisfied by a real endpoint
   - leaf is addressable by a stable **node path** inside the props tree

### Binding primitives (Minimal API extensions)

- **`ForTsxFile(string path)`**
  - binds a route/component node to the TSX renderer file
  - path must be under `Features/` and end in `.tsx` (enforced)

- **`ForNode<TProps>(Expression<Func<TProps, object>> path)`**
  - binds a mode endpoint to the exact leaf inside the props tree
  - leaf path is canonicalized (e.g. `Queue.Grid`, `RightRail.Notifications`)
  - this is how we answer: “if a tree component is deferred, from where does it load?”

### “Endpoint in tree must exist” rule

For each route node `TProps`, the generator walks the props tree:

- `Component<TChildProps>` → descend into `TChildProps`
- `Deferred<T>`, `Lazy<T>`, `Stream<T>`, `Sse<T>` → leaf requirement

Each leaf must have a matching endpoint:

- endpoint has `.ForComponent<TProps>()`
- endpoint has `.ForNode<TProps>(...)` matching the leaf path
- endpoint has the corresponding mode `.AsDeferred<T>()` / `.AsLazy<T>()` / etc.

If any leaf is not satisfied by a real endpoint → generator diagnostic (build fails).

## Generation targets (TanStack “full power”)

### 1) RouterContext-typed route tree

- Generate root route with `createRootRouteWithContext<AppContext>()`.
- Generate route defs that import TSX renderers from `ForTsxFile`.
- Never edit generated route defs.

### 2) Generated loaders (tree-aware)

Route loaders are generated from the tree:

- seed immediate props from `__SHALIMAR_PROPS__`
- register leaf handles into stores
- optional loader policies:
  - prefetch deferred after navigation
  - lazy only on intent
  - do not auto-start SSE unless policy requires it

### 3) Generated stores (TanStack Store)

For each component key / props type:

- `ui` slice: stable view state (not invalidated)
- `data` slice: derived from mode caches keyed by href/prefix
- selectors for leaf paths

### 4) Generated mutations

- Generator emits `useMutations()` and mutation specs:
  - defaults
  - schema (hints only)
  - mutate function
  - invalidations (prefix graph)
- Runtime provides `useMutation(spec)` to execute and surface `{busy, validation, error}`.

## TDD plan (function-level)

### Phase A: Metadata + diagnostics

- Implement `ForTsxFile` and `ForNode` metadata (C#).
- Generator reads metadata and emits diagnostics for:
  - invalid TSX paths
  - missing leaf bindings
  - ambiguous bindings

Tests:
- generator unit tests that missing leaf → diagnostic
- generator unit tests that `ForNode` binding resolves

### Phase B: Generate TS route bindings (safe incremental step)

- Emit a generated file describing TSX bindings and leaf bindings.
- Keep existing runtime behavior unchanged until the binding graph is fully validated.

### Phase C: Generate route modules + virtual route config

- Generate route modules under `Generated/Routes/**` which:
  - call `createFileRoute`
  - import and render the TSX renderer from `ForTsxFile`
  - attach generated loader and search validation
- Generate `shalimar-routes.g.ts` to point at these generated modules (not `Features/**/route.tsx`).

### Phase D: Generate stores

- Generate per-props store modules and selectors derived from leaf paths.

### Phase E: Sandbox app “Agent Workbench” proving slice

- One slice proving:
  - grid with paging + external filters (URL state)
  - right drawer create/edit
  - nested FluentValidation keys (`Tags[1]`, etc.)
  - invalidation refresh preserves URL state
  - deferred/lazy/stream/sse behavior

## Guardrails to prevent bloat

- No hand-authored route modules in `Features/**/route.tsx` once Phase C lands.
- Playwright tests assert behavior via `data-testid` and URL state only.
- `verify.sh` stays green at every phase boundary.

