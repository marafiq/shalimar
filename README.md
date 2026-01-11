# Shalimar

> Unlocks React echosystem without writing SPA or involving NodeJS in production. 

---

## What is Shalimar?

Shalimar is a .NET 10 + React framework where **server defines everything** and a **Roslyn source generator produces all the TypeScript**. You write C# records, Minimal APIs, and FluentValidation — Shalimar generates typed hooks, Zod schemas, route trees, and form defaults.

---

## Getting Started

```bash
dotnet new shalimar -n MyApp
cd MyApp
dotnet run
```

Open `https://localhost:5001` — you're running with HMR.

---

## Zero-step restore/build/test (framework repo)

This repo is designed to support TDD for **every component** (Runtime, SourceGenerator, RoslynAnalyzer, MSBuildTasks, Vite) with unit tests, plus an end-to-end **template → sandbox app → Playwright** pipeline.

### Canonical workflow (do not reinvent)

- **Bootstrap once per machine** (installs `pwsh`, the `.NET SDK` pinned by `global.json`, and `bun` into user-local folders):

```bash
bash ./scripts/bootstrap.sh
export PATH="$HOME/.pwsh:$HOME/.dotnet:$HOME/.bun/bin:$PATH"
```

- **Then every time** (single source of truth for restore/build/test/integration/E2E):

### One command (Linux/macOS)

```bash
bash ./scripts/verify.sh
```

### One command (PowerShell)

```powershell
./scripts/verify.ps1 -VerifyTsPropagation
```

What it does:
- **restore**: `dotnet restore` + `bun install`
- **build**: `dotnet build`
- **unit tests**: runs each unit test project under `tests/` (excludes Playwright by design)
- **integration pipeline**: pack local NuGets → `dotnet new shalimar` → build the sandbox app → run Playwright

Playwright artifacts:
- **always**: `tests/TestResults/playwright/<TestName>/{console.log,page-errors.log}`
- **on failure**: `failure.png`, `failure.html`, `exception.txt`

Override root via `SHALIMAR_TEST_ARTIFACTS`.

### Proof that runtime TypeScript is delivered via the template (no manual copying into the generated app)

`verify` includes an explicit proof step:
- It temporarily appends a unique marker comment to `src/Shalimar.Runtime/ts/src/index.ts`
- Packs the template
- Runs `dotnet new shalimar`
- Verifies the marker exists in the generated app at `Shared/runtime/index.ts`
- Restores the original `index.ts` (no repo changes left behind)

---

## Two developer experiences

### Framework developers (local NuGet workflow)

- Build and pack local packages:

```bash
pwsh ./scripts/pack.ps1 -Version 1.0.0-local
```

- Use the local `artifacts/` folder as a package source (the integration pipeline generates an app-local `nuget.config` that points at `artifacts/`).

### End consumers (`dotnet new shalimar`)

- Install the template (from NuGet, or from a local `.nupkg` during development) and create an app:

```bash
dotnet new install Shalimar.Templates
dotnet new shalimar -n MyApp
```

---

## Sandbox app dev loop (HMR)

For framework development, the ideal local loop is:
- keep the app running in **Development**
- keep Vite running with **HMR**
- iterate on framework packages, then re-run `scripts/integration.ps1 -SkipPack` (or `scripts/verify` if you want a full reset).

---

## Features

**Zero Magic Strings**  
Routes, props, mutations, invalidations — generated and typed on both C# and TypeScript.

**Server-Driven Data Modes**  
Immediate, Deferred, Lazy, Streamed — declared in C#, React Suspense handles the rest.

**Real-time SSE**  
Typed Server-Sent Events with discriminated unions and generated subscription hooks.

**FluentValidation → Zod**  
Server validation rules generate client schemas and form defaults automatically.

**Mutations with Typed Invalidation**  
`.Invalidates<T>()` generates client-side cache invalidation.

**HMR in Development**  
Vite-powered hot module replacement. Edit C# → types regenerate → React updates.

**Hashed Assets in Production**  
Production builds output fingerprinted bundles. No Node.js server required — .NET serves static assets.

**No Node.js Runtime**  
Vite for dev tooling only. Production is pure .NET serving static files.

---

## What Gets Generated

| You Write (C#) | C# Generated | TypeScript Generated |
|----------------|--------------|---------------------|
| `MapGet("/posts/{id}", ...)` | `Routes.Posts.Detail(id)` | `Routes.posts.detail(id)` |
| `record PostProps(...)` | — | Interface + `usePost()` hook |
| `AbstractValidator<T>` | — | Zod schema + defaults |
| `.AsComponent<T>()` | `Components.Posts.Detail` | Route tree entry |
| `.AsMutation<TReq, TRes>()` | — | `useCreatePost()` hook |
| `.Invalidates<T>()` | — | Typed invalidation |
| `.SubscribeTo<TEvent>()` | — | `useCommentsSubscription()` hook |

```
Roslyn Source Generator
         │
         ├──► C#: Routes.g.cs
         │
         └──► TypeScript (embedded, extracted by MSBuild):
              ├── shalimar-types.g.ts
              ├── shalimar-zod-schemas.g.ts
              ├── shalimar-defaults.g.ts
              ├── shalimar-tanstack-virtual-file-routes.g.ts
              ├── shalimar-hooks.g.ts
              ├── shalimar-mutations.g.ts
              └── shalimar-subscriptions.g.ts
```

---

## Data Modes


| Mode | Server | Client |
|------|--------|--------|
| Immediate | Evaluated before response | Props on first render |
| Deferred | Fetched after hydration | Suspense boundary |
| Lazy | Fetched when triggered | `load()` function |
| Streamed | Chunked response | Progressive render |

---

## Why the name "Shalimar"?

Named after **Shalimar Gardens** in Lahore.

---

## Status

Under active development. Targeting .NET 10.
