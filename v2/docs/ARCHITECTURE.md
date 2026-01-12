# Shalimar v2 architecture (detailed)

This document describes the **current “v2 clean-slate” architecture** as implemented in this repository: **server-first**, **explicit**, and **generated client wiring** (TanStack Router + TanStack Store), with **Spectrum S2 + Tailwind** as the only UI stack.

It is written for framework authors working on Shalimar (not just consumers using `dotnet new shalimar`).

---

## 1) Non‑negotiables (v2)

- **Server defines everything**: routes, RouterContext, props trees, modes, mutations, invalidations.
- **Server-first rendering (not SPA)**:
  - Browser requests go to ASP.NET Core first.
  - The server returns HTML + `__SHALIMAR_CONTEXT__` + `__SHALIMAR_PROPS__`.
  - Client-side TanStack Router is used for DX and internal navigation, but the server remains the source of truth.
- **Generated client wiring**:
  - No handwritten TanStack route modules.
  - No handwritten fetch URLs in TSX.
  - Generated stores/selectors/mutations are the only app-facing API.
- **UI stack**: `@react-spectrum/s2` + Tailwind only.
- **Routing**: TanStack **virtual file routes only** (via a generated virtual route config).
- **State**: TanStack Store only (`@tanstack/store` + `@tanstack/react-store`).
- **Validation**: FluentValidation is authoritative. Client schemas are hints/ergonomics only.
- **Quality gates**: one “zero-step” pipeline plus unit + Playwright tests with screenshots.

---

## 2) Repository-level delivery model

### 2.1 Packages

Shalimar is built as several NuGet packages plus a template:

- `Shalimar.Runtime` (net10.0): ASP.NET Core runtime + the TypeScript runtime sources.
- `Shalimar.SourceGenerator` (netstandard2.0): Roslyn generator producing C# + TS.
- `Shalimar.MSBuildTasks` (netstandard2.0): extracts embedded TS into `Generated/`.
- `Shalimar.RoslynAnalyzer` (netstandard2.0): optional compile-time rules.
- `Shalimar.Vite` (net10.0): dev server integration (HMR only).
- `Shalimar.Templates`: `dotnet new shalimar` template.

### 2.2 The sandbox app (generated)

`Shalimar.SandboxApp` is created from the template by the integration pipeline and is **gitignored**.

The sandbox app exists to prove:

- the template experience,
- runtime TypeScript delivery,
- server-first routing,
- generated client wiring,
- end-to-end behavior and screenshots.

---

## 3) Zero-step pipeline (the “one true” way)

The canonical entrypoint is:

- Linux/macOS: `bash ./scripts/verify.sh`
- PowerShell: `./scripts/verify.ps1 -VerifyTsPropagation`

The pipeline performs:

1) **Restore**
- `dotnet restore`
- `bun install`

2) **Build**
- `dotnet build` (warnings-as-errors in practice: build must be clean)

3) **Unit tests**
- `pwsh ./scripts/test.ps1 -Unit`

4) **Integration pipeline**
- pack all packages to `artifacts/`
- install template from `artifacts/`
- `dotnet new shalimar -n Shalimar.SandboxApp`
- build sandbox app + Vite production build (hashed assets)
- run Playwright (unless explicitly skipped)

### 3.1 Proof: runtime TS is delivered without manual copying

The integration script performs an explicit proof:

- temporarily appends a unique marker to `src/Shalimar.Runtime/ts/src/index.ts`
- packs the template
- generates the sandbox app
- asserts the marker exists at `src/Shalimar.SandboxApp/Shared/runtime/index.ts`
- restores the original file (no dirty working tree)

This ensures the runtime TypeScript delivery path is **reliable** and does not depend on “copy this folder manually” steps.

---

## 4) Server-first request flow

### 4.1 Request lifecycle (high level)

```
Browser GET /v2/tasks
  │
  ├──► ASP.NET Core endpoint (Minimal API)
  │      - composes server-authored props tree
  │      - returns HTML shell
  │      - injects __SHALIMAR_CONTEXT__ and __SHALIMAR_PROPS__
  │
  └──► Client hydrates
         - TanStack Router route loader reads __SHALIMAR_PROPS__
         - generated store is set
         - TSX page renders by reading the store (no props arg)
         - Deferred boundaries may suspend and fetch via generated hooks
```

### 4.2 App context

Server builds an `AppContextModel` which is injected into the HTML as `__SHALIMAR_CONTEXT__`.

The generated TanStack Router root route is typed with that context (`createRootRouteWithContext<TContext>()`),
so **client route components read a typed RouterContext**, not global variables.

---

## 5) The v2 contract: server-authored props tree

v2 formalizes the “server defines everything” rule as a **typed contract tree**:

- A route endpoint returns a props record `TProps : IComponentProps`.
- That record can contain nested `Component<TChildProps>` nodes.
- Leaves are **mode handles**:
  - `Deferred<T>`
  - `Lazy<T>`
  - `Stream<T>`
  - `Sse<T>`

### 5.1 Node types

**Route node (Immediate)**  
A Minimal API route mapped with:

- `.ForTsxFile("Features/V2/.../Page.tsx")`
- `.AsComponent<TProps>()`

It returns the initial props tree (which may include mode handles).

**Component node (Immediate, non-route)**  
`Component<TChildProps>` is an explicit composition wrapper allowing a parent props model to embed a child component’s props (and its mode handles) without magic strings.

**Mode leaf node**  
A mode leaf is a handle which points at a real endpoint:

- `Deferred<T>(Href)`: fetched after hydration and typically used behind `<Suspense>`.
- `Lazy<T>(Href)`: fetched only when the user triggers `load()`.
- `Stream<T>(Href)`: finite stream (NDJSON) read progressively; does not start by default.
- `Sse<T>(Href)`: realtime subscription (EventSource); does not start by default.

### 5.2 Binding primitives (Minimal API extensions)

Shalimar uses explicit Minimal API “metadata extensions” to declare the contract:

- **`.ForTsxFile(string path)`**  
  Binds a route/component endpoint to a TSX renderer module (a “pure TSX slice”).

- **`.ForComponent<TProps>()`**  
  Declares the root props type whose tree this endpoint participates in.

- **`.ForNode<TProps>(p => p.X.Y.Z)`**  
  Binds a mode endpoint to a specific leaf path in the `TProps` tree.
  Canonicalization rule: `Component<T>.Props` segments are ignored for matching (the tree is conceptually “flattened” across component wrappers).

- **`.AsComponent<TProps>()`**  
  Marks a route endpoint as a component route; it becomes a source-of-truth input to generation.

- **`.AsDeferred<T>() / .AsLazy<T>() / .AsStream<T>() / .AsSse<T>()`**  
  Marks a leaf endpoint as fulfilling a corresponding handle.

### 5.3 Generator-enforced binding rules

At build time, the generator:

1) Finds every endpoint with `.AsComponent<TProps>()`.
2) Walks the `TProps` tree to discover every mode leaf.
3) Ensures each leaf has exactly one matching endpoint:
   - same root `.ForComponent<TProps>()`
   - matching `.ForNode<TProps>(...)`
   - matching mode (`AsDeferred/AsLazy/AsStream/AsSse`)

Violations are diagnostics (build-breaking):

- **Missing leaf binding** (e.g., `Deferred<T>` exists in the tree but no endpoint binds it).
- **Duplicate leaf binding** (multiple endpoints bind the same leaf).

This is the main mechanism that prevents “works on my machine” wiring drift.

---

## 6) Generated TypeScript: what exists, where, and why

Shalimar generates TypeScript into the sandbox app’s `Generated/` directory (extracted by MSBuild from generator-embedded sources).

### 6.1 Router generation (TanStack virtual file routes)

Inputs:

- Server endpoints + their `.ForTsxFile(...)` bindings.

Outputs:

- **`Generated/shalimar-routes.g.ts`**: TanStack Router virtual route config mapping route paths to modules.
- **`Generated/V2Routes/**/route.tsx`**: generated route modules for v2 routes.
- **`Generated/routeTree.gen.ts`**: generated by `@tanstack/router-plugin/vite` (not by Shalimar) from the virtual config.

Hard rule:

- `createFileRoute(...)` must exist only inside generated route modules.

### 6.2 v2 store generation (TanStack Store)

v2 generates a store model keyed by root props type name:

- `v2Keys.<TypeName>` is the **single generated constant** used everywhere instead of repeating string literals.
- Each v2 store is a `V2RouteState<TProps>`:

```
{ props: TProps | null, ui: Record<string, unknown> }
```

The key design point is **UI state survives invalidation and props refresh**.

Generated API surface (conceptually):

- `setV2Props(key, props)` (called by generated route loaders)
- `useV2Props(key)` / `getV2Props(key)`
- `useV2Ui(key)` / `mergeV2Ui(key, patch)`

### 6.3 v2 selectors generation (typed data hooks)

For each leaf handle in the server tree, v2 generates a typed hook that:

1) Reads the current props from the v2 store.
2) Reads the leaf handle’s `href`.
3) Calls the runtime mode hook with that `href`.

Examples (conceptual):

- `useV2TasksPropsGridDeferred(): CrmTasksGridDto`
- `useV2LivePropsTimelineLazy(): { status, data, load, reset }`
- `useV2LivePropsRealtimePanelAuditStream(opts): { status, items, start, abort, reset }`
- `useV2LivePropsRealtimePanelNotificationsSse(opts): { status, events, start, stop, reset }`

Note: Stream/SSE hooks intentionally do **not auto-start**; the UI must call `start()`.

### 6.4 Generated invalidation helpers

Invalidation uses the runtime’s **prefix-based cache invalidation**:

- `invalidateDeferredByPrefix(prefix)`
- `invalidateLazyByPrefix(prefix)`
- `invalidateStreamedByPrefix(prefix)`
- `invalidateSseByPrefix(prefix)`

For v2, helpers are generated per leaf (e.g., `invalidateV2TasksPropsGrid()`), reading the current `href` from the v2 store and invalidating by prefix.

This keeps invalidation logic:

- server-authored (declared via `.Invalidates<T>()` and leaf handles),
- client-reliable (no hand-maintained cache keys in TSX).

---

## 7) Runtime mode implementations (client)

The runtime is intentionally minimal, predictable, and cached-by-href.

### 7.1 Deferred

- **Behavior**: fetch JSON by `href` and **suspend** (via React Suspense semantics).
- **Cache**: keyed by `href`.
- **Invalidation**: `invalidateDeferredByPrefix(prefix)` removes all entries matching prefix (+ query variants).

### 7.2 Lazy

- **Behavior**: does not fetch automatically; exposes `load()` to fetch JSON.
- **Cache**: keyed by `href`.
- **Invalidation**: `invalidateLazyByPrefix(prefix)`.

### 7.3 Stream (finite)

- **Transport**: NDJSON over HTTP (`application/x-ndjson`).
- **Behavior**: does not start automatically; `start()` begins reading NDJSON lines into a bounded list.
- **Cancellation**: `AbortController` (via `abort()`).
- **Invalidation**: `invalidateStreamedByPrefix(prefix)` aborts matching streams and clears cache.

### 7.4 SSE (realtime)

- **Transport**: EventSource (`text/event-stream`).
- **Behavior**: does not connect automatically; `start()` opens an EventSource; `stop()` closes it.
- **Cache**: keyed by `href` so multiple components share a single connection.
- **Invalidation**: `invalidateSseByPrefix(prefix)` closes matching connections and clears cache.

---

## 8) Mutations + server-truth validation

### 8.1 Server

Mutations are Minimal API endpoints marked with:

- `.AsMutation<TReq, TRes>()`
- (optionally) `.Invalidates<TProps>()` for server-declared invalidation targets

FluentValidation is authoritative:

- on validation failure: server returns `ValidationProblem` with errors keyed by property path (including nested paths like `Tags[1]`).

### 8.2 Client

The client uses generated mutation specs and a thin runtime submit helper:

- generated: `useMutations()` returns typed specs (defaults, schema hints, endpoint binding).
- runtime: `useMutationSubmit(spec)` handles:
  - submit
  - busy state
  - surfacing server validation errors
  - surfacing server error state

The v2 proof page `/v2/tasks` demonstrates:

- submit with missing Title → server returns validation error → UI shows it
- fill Title → success → toast → invalidate grid leaf without navigation

---

## 9) “Pure TSX vertical slice” rules (what TSX may and may not do)

In v2 `Features/V2/**` TSX files:

Allowed:

- render UI with S2 + Tailwind
- read RouterContext (typed) as needed
- read props via `use{Root}Props()` and leaf hooks via generated selectors
- call generated mutation specs via runtime submit helper
- store UI-only state via `mergeV2Ui(...)` (not bespoke global caches)

Forbidden:

- `createFileRoute(...)`
- direct use of `window.__SHALIMAR_PROPS__`
- hard-coded fetch URLs for deferred/lazy/stream/sse endpoints
- custom caching layers (repos, query clients, etc.)

---

## 10) Concrete v2 proof routes in the sandbox template

These exist to prove the architecture end-to-end:

### 10.1 `/v2/workbench`

- Demonstrates: nested `Component<T>` with deferred leaves.
- TSX reads from generated store + hooks; server owns prefetch policy.

### 10.2 `/v2/tasks`

- Demonstrates: table paging + external filters + create pane + invalidation refresh.
- Demonstrates: URL-driven server-first routing plus UI state surviving refresh (v2 UI store).

### 10.3 `/v2/live`

- Demonstrates: Deferred + Lazy + Stream + SSE in one screen.
- Guarantees: Stream/SSE never auto-start; must be started by the user (and tested).

---

## 11) Testing strategy (TDD quality gates)

### 11.1 Unit tests

Unit tests exist for:

- runtime (mode behavior, cache/invalidation contracts)
- generator (tree walking, diagnostics, output snapshots)
- analyzer/MSBuild tasks

These are the primary place to evolve correctness rules.

### 11.2 Playwright integration tests

Playwright proves the “real system”:

- template → generated sandbox app boots
- server returns valid HTML shells for all routes (`__SHALIMAR_CONTEXT__`, `__SHALIMAR_PROPS__`)
- UI behavior flows work (validation, mutation, invalidation, navigation)
- screenshots (visual regression) are stored under:
  - `tests/Shalimar.IntegrationPlaywrightTests/Snapshots/*.png`

Observability:

- browser console logs and errors are captured under test artifacts.

---

## 12) How to extend v2 correctly (author workflow)

When adding a new v2 feature:

1) Define a server props record under `Features/V2/<Feature>/...Props.cs`.
2) Add a route endpoint returning that props record:
   - `.ForTsxFile(...)`
   - `.AsComponent<TProps>()`
3) Add leaf endpoints for each `Deferred/Lazy/Stream/Sse` handle:
   - `.ForComponent<TProps>()`
   - `.ForNode<TProps>(...)`
   - `.AsDeferred/.AsLazy/.AsStream/.AsSse`
4) Implement TSX as a pure renderer using generated store/selectors/mutations.
5) Add Playwright coverage + snapshots if the feature is user-visible.

If a leaf is missing or duplicated, the build should fail with a generator diagnostic.

