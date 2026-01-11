# Shalimar Development Skill

Concise reference for AI-assisted Shalimar framework development.

---

## Prerequisites

Install these before using the harness scripts:

| Tool | Install Command | Verify |
|------|-----------------|--------|
| **.NET 10 SDK** | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) | `dotnet --version` → 10.x |
| **PowerShell** | `brew install powershell` (macOS) or [Download](https://github.com/PowerShell/PowerShell) | `pwsh --version` |
| **bun** | `curl -fsSL https://bun.sh/install \| bash` | `bun --version` |

**macOS quick setup:**
```bash
# Install Homebrew if needed
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# Install all prerequisites
brew install --cask dotnet-sdk
brew install powershell/tap/powershell
curl -fsSL https://bun.sh/install | bash
```

**Windows quick setup:**
```powershell
# Using winget
winget install Microsoft.DotNet.SDK.10
winget install oven-sh.Bun
# PowerShell 7+ usually pre-installed, or: winget install Microsoft.PowerShell
```

---

## Delivery Model

```
ALL VIA NUGET
─────────────────────────────────────────────────
Shalimar.Templates.nupkg
├── Shalimar.Runtime (C# + embedded TypeScript)
├── Shalimar.SourceGenerator
├── Shalimar.MSBuildTasks
├── Shalimar.RoslynAnalyzer
└── Shalimar.Vite

End user: dotnet new shalimar -n MyApp && dotnet run
```

---

## Project Structure

```
src/
├── Shalimar.Runtime/          # Unified runtime package
│   ├── ts/src/                # TypeScript source (@shalimar/runtime)
│   ├── *.cs                   # C# ASP.NET Core extensions
│   └── Shalimar.Runtime.csproj
├── Shalimar.SourceGenerator/  # Roslyn incremental generator (netstandard2.0)
├── Shalimar.MSBuildTasks/     # MSBuild task for TypeScript extraction
├── Shalimar.RoslynAnalyzer/   # Roslyn analyzer (netstandard2.0)
├── Shalimar.Vite/             # Vite dev server integration
├── Shalimar.Templates/        # dotnet new template package
│   └── templates/shalimar/Shared/runtime/  # runtime TS destination in generated app
└── Shalimar.SandboxApp/      # Generated sandbox app (gitignored)

tests/
├── Shalimar.Runtime.Tests/
├── Shalimar.SourceGenerator.Tests/
├── Shalimar.RoslynAnalyzer.Tests/
└── Shalimar.IntegrationPlaywrightTests/
```

---

## Harness Commands

### Restore
```bash
./scripts/restore.ps1
```
**What it does:**
1. `dotnet restore Shalimar.slnx` - Restores all .NET packages
2. `bun install` - Installs TypeScript dependencies (workspace)

**Logs:** Output directly to console. Errors show which package/dependency failed.

### Build
```bash
./scripts/build.ps1                    # Debug build
./scripts/build.ps1 -Configuration Release
```
**What it does:**
1. `dotnet build Shalimar.slnx` - Builds all .NET projects

**Logs:** .NET build output shows warnings/errors with file:line

### Unit Tests
```bash
./scripts/test.ps1 -Unit
```
**What it does:** Runs all xUnit tests excluding Integration category.

**Components tested:**
- `Shalimar.Runtime.Tests` - ShellRenderer, extensions
- `Shalimar.SourceGenerator.Tests` - Code generation
- `Shalimar.RoslynAnalyzer.Tests` - Analyzer diagnostics

**Logs:** VSTest output with pass/fail counts. Failed tests show:
```
Failed TestName [duration]
  Error Message: ...
  Stack Trace: ...
```

### Integration Tests
```bash
./scripts/integration.ps1              # Full pipeline
./scripts/integration.ps1 -SkipPack    # Use existing artifacts
./scripts/integration.ps1 -SkipTests   # Build only, no Playwright
```
**What it does:**
1. Packs all NuGet packages to `artifacts/`
2. Clears NuGet cache for shalimar packages
3. Installs template from local artifacts
4. Creates sandbox app via `dotnet new shalimar`
5. Builds sandbox app (dotnet + bun/vite)
6. Runs Playwright E2E tests

**Logs:**
- Pack output shows each .nupkg created
- Build shows generated files verification
- Playwright output shows test pass/fail with timing

### Pack Only
```bash
./scripts/pack.ps1                     # Version 1.0.0-local
./scripts/pack.ps1 -Version 2.0.0
```
**What it does:**
1. Builds all .NET projects (Release)
2. Creates NuGet packages in `artifacts/`

**Runtime TS delivery (single source of truth):**
- Template packages the runtime TS directly from `src/Shalimar.Runtime/ts/src/` into the template output path `Shared/runtime/` in the generated app.
- There should be **no manual copying** of runtime TS into the template folder as part of the pack step.

**Logs:** Lists all .nupkg files created with paths.

---

## Generated Files

After `dotnet build`:
```
Generated/
├── shalimar-routes.g.ts       # TanStack virtual routes
├── shalimar-route-defs.g.ts   # Route definitions object
├── shalimar-types.g.ts        # TypeScript interfaces from C#
└── shalimar-paths.g.ts        # Type-safe path helpers
```

After `bun run build`:
```
Generated/routeTree.gen.ts     # TanStack Router tree
wwwroot/dist/.vite/manifest.json
```

---

## Source Generator Flow

```
1. ShalimarGenerator finds AsComponent<T>() calls
2. Generates .g.cs with TypeScript in block comments:
   /* SHALIMAR_TS: filename.g.ts
      ... TypeScript content ...
   END_SHALIMAR_TS */
3. MSBuild task extracts TypeScript to Generated/ folder
4. TanStack Router plugin reads routes, generates routeTree
```

---

## Playwright E2E Tests

| Test | Validates |
|------|-----------|
| Shell_Contains_Context | `__SHALIMAR_CONTEXT__` in HTML |
| Shell_Contains_Version | `__SHALIMAR_VERSION__` in HTML |
| Shell_Contains_Environment | AppContext.Environment |
| Shell_Has_Hashed_Assets | `/dist/assets/` paths |
| Home_Renders | `<h1>Welcome` element |
| No_Console_Errors | Zero console errors |

---

## NOT an SPA

```
✅ Shalimar                       ❌ SPA
───────────────────────────────   ───────────────────────────
Browser → .NET → shell            Browser → Vite → index.html
Browser → .NET → data             Browser → Vite → proxy → API
Browser → Vite → JS only (HMR)
```

---

## Versions (Latest January 2026)

| Package | Version |
|---------|---------|
| .NET SDK | **10.0.101** |
| C# | **14** |
| React | **19.2.3** |
| TanStack Router | **1.147.1** |
| TanStack Store | **0.8.0** |
| Vite | **7.3.1** |
| TypeScript | **5.9.3** |
| Playwright | **1.57.0** |
| bun | **latest** |

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Old package cached | Delete `~/.nuget/packages/shalimar*` |
| Generated missing | Check `dotnet build` output for errors |
| routeTree missing | Check `bun run build` output |
| Playwright fails | Run `./scripts/integration.ps1` (creates sandbox app) |
| bun workspace error | Delete `bun.lockb`, run `bun install` |
| slnx not found | Ensure using .NET 10 SDK |

---

## Log Locations

| Component | Log Location |
|-----------|--------------|
| dotnet restore/build | Console output (stderr for errors) |
| bun install/build | Console output |
| Source Generator | `obj/Debug/net10.0/generated/` (EmitCompilerGeneratedFiles) |
| MSBuild Task | Console output during build |
| Playwright | Console output + `TestResults/` folder |
| Sandbox app | Console when running `dotnet run` |

---

## Quick Verification

```bash
# Full harness (recommended)
./scripts/integration.ps1

# Expected output:
# ✓ 6 NuGet packages in artifacts/
# ✓ 6 generated files verified
# ✓ 6 Playwright tests passed
```
