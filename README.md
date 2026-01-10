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
