#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP="$ROOT/src/Shalimar.SandboxApp"

echo "==> Restore"
pwsh "$ROOT/scripts/restore.ps1"

echo "==> Build (generates TS into Generated/)"
(cd "$APP" && dotnet build)

echo "==> Build frontend assets to wwwroot/dist"
(cd "$APP" && bun install && bun run build)

echo "==> Run (Production env, uses hashed assets)"
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:5099}"

echo "Open: $ASPNETCORE_URLS"
(cd "$APP" && dotnet run --no-build)

