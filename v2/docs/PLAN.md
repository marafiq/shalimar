# Shalimar v2 (clean-slate) plan — server-defined tree → generated TanStack

This folder (`/v2`) is the **only** place v2 work lives. We may reference existing repo code for ideas, but we do not incrementally refactor v1. v2 is built as a clean implementation with TDD quality gates.

---

## Non‑negotiables (v2)

- **Server defines everything**: routes, RouterContext, props trees, modes, mutations, invalidations.
- **Generated client wiring**: TanStack Router route tree + route modules + loaders + stores are generated.
- **Never edit generated files** (anything under a `Generated/` output).
- **S2 + Tailwind only** for UI (shadcn is inspiration, not dependency).
- **TanStack virtual file routes only** (via `virtualRouteConfig`).
- **TanStack Store only** for state.
- **Server-truth validation**: client never blocks mutation submit due to client schema; FluentValidation drives authoritative errors.
- **TDD** at function level:
  - generator/runtime code must be introduced via unit tests first
  - Playwright only validates behavior (testids/roles/URL state), never drives structure.

---

## v2 goal: “pure TSX vertical slices”

In v2, a feature TSX component should only:
- render UI with S2/Tailwind
- read typed RouterContext
- read typed selectors from generated stores
- call `useMutations()` + runtime `useMutation(spec)`

It should **not**:
- create routes
- build loaders
- build fetch URLs
- implement caching/invalidation rules

---

## Key design: server-authored contract graph (“tree”)

### Node kinds

1) **Route node (Immediate)**
- has a URL path
- has TSX renderer binding
- returns *immediate* props (may contain handles)

2) **Component node (Immediate, non-route)**
- TSX renderer binding
- composed into other props

3) **Mode leaf node**
- Deferred/Lazy/Streamed/SSE
- leaf is satisfied by a **real Minimal API endpoint**

### Binding primitives (Minimal API extensions)

- `ForTsxFile("Features/V2/.../*.tsx")`
  - binds a route/component node to a TSX module

- `ForNode<TProps>(p => p.X.Y.Z)`
  - binds a mode endpoint to a specific leaf path in the `TProps` tree
  - canonicalizes by dropping `Component<T>.Props` segment

### Endpoint‑exists rule

For each `AsComponent<TProps>()` route node, the generator walks the `TProps` tree:
- `Component<TChild>`: descend
- `Deferred<T> / Lazy<T> / Stream<T> / Sse<T>`: leaf requirement

Each leaf must be satisfied by an endpoint with:
- `.ForComponent<TProps>()`
- `.ForNode<TProps>(...)` matching leaf path
- `.AsDeferred<T>()` / `.AsLazy<T>()` / `.AsStream<T>()` / `.AsSse<T>()`

If any leaf is missing a binding → **diagnostic error** (build fails).

---

## Generation targets (what v2 generator must emit)

### 1) RouterContext-typed root
- `createRootRouteWithContext<AppContext>()` in generated route root

### 2) Generated route modules (no handwritten route wiring)
- emit route modules under `Generated/V2Routes/**`
- each module calls `createFileRoute(...)` and renders the bound TSX
- no route modules exist under `Features/V2/**` (those are pure components)

### 3) Generated loaders from the tree
- register immediate props into stores
- register leaf handles for current route
- execute prefetch policies (deferred prefetch after navigation)
- never embed UI logic

### 4) Generated TanStack stores/selectors
- per component key and leaf path
- split `{ uiState, dataState }` so invalidation never nukes UI state

### 5) Generated mutations
- `useMutations()` returns typed mutation specs:
  - defaults (generated)
  - schema (generated, hints only)
  - `mutate` function
  - invalidation targets (component keys / prefixes)
- runtime `useMutation(spec)` is the only submit path

---

## TDD roadmap (step-by-step)

### Phase A — metadata + diagnostics (unit tests first)
Implement v2 metadata types and extraction:
- `ForTsxFile`, `ForNode`, `AsComponent`, `ForComponent`, mode metadata
Tests:
- `ForTsxFile` affects generated route config
- `ForNode` path extraction canonicalizes `.Props`
- missing leaf binding produces generator diagnostic

### Phase B — tree walker + binding validator
Implement:
- props tree walker (records, `Component<T>`, mode handles)
- endpoint binding matcher
Tests:
- complex nested props tree requires matching endpoints
- duplicate/multiple endpoints bound to same leaf => diagnostic

### Phase C — generate v2 route modules + virtual route config
Implement:
- generator emits `Generated/V2Routes/**` modules
- generator emits `shalimar-v2-routes.g.ts` that points to generated modules
Tests:
- snapshot generation stable
- Vite build works with `virtualRouteConfig` pointing at v2 routes

### Phase D — generate loaders + stores
Implement:
- minimal store generation per route node
- loader generation from leaf policies
Tests:
- invalidation preserves UI slice
- deferred prefetch respects policy and does not start SSE by default

### Phase E — v2 sandbox app domain: Agent Workbench
Server:
- endpoints + FluentValidation with nested `RuleForEach` (e.g. `Tags[1]`, `Decisions[0].Options[1]`)
Client:
- `Features/V2/**` pure components using generated stores/mutations
E2E:
- create flow shows nested validation and succeeds
- grid refresh via invalidation preserves URL state

---

## Guardrails (to prevent bloat)

- No `createFileRoute` calls outside generated route modules.
- No fetch URL strings in TSX.
- No mutation calls except via `useMutations()` + runtime `useMutation`.
- Playwright tests assert behavior only (testids/URL/server HTML contract).

