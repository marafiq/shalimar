#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if command -v pwsh >/dev/null 2>&1; then
  exec pwsh -NoLogo -NoProfile -File "${repo_root}/scripts/verify.ps1" "$@"
fi

die() {
  echo "Error: $*" >&2
  exit 1
}

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "Missing prerequisite '$1'. $2"
}

# PowerShell is optional on Linux; this script provides a native bash fallback.
need_cmd dotnet "Install the .NET SDK pinned by global.json."
need_cmd bun "Install bun (https://bun.sh/) for Vite/TS tooling."
need_cmd curl "Install curl (used for readiness checks)."

skip_integration="false"
skip_e2e="false"
version="1.0.0-local"
configuration="Debug"
port="5099"

args=("$@")
i=0
while [ $i -lt ${#args[@]} ]; do
  a="${args[$i]}"
  case "$a" in
    -SkipIntegration|--skip-integration)
      skip_integration="true"
      ;;
    -SkipE2E|--skip-e2e)
      skip_e2e="true"
      ;;
    -Version|--version)
      i=$((i+1))
      version="${args[$i]:-}"
      [ -n "$version" ] || die "Missing value for $a"
      ;;
    -Configuration|--configuration)
      i=$((i+1))
      configuration="${args[$i]:-}"
      [ -n "$configuration" ] || die "Missing value for $a"
      ;;
    -Port|--port)
      i=$((i+1))
      port="${args[$i]:-}"
      [ -n "$port" ] || die "Missing value for $a"
      ;;
    *)
      die "Unknown argument: $a"
      ;;
  esac
  i=$((i+1))
done

echo "Shalimar verify (bash fallback)..."
echo "  Repo: ${repo_root}"
echo "  Configuration: ${configuration}"
echo "  Version: ${version}"
echo "  SkipIntegration: ${skip_integration}"
echo "  SkipE2E: ${skip_e2e}"

echo
echo "[1/4] Restore"
dotnet restore "${repo_root}/Shalimar.slnx"
(cd "${repo_root}" && bun install)

echo
echo "[2/4] Build"
dotnet build "${repo_root}/Shalimar.slnx" -c "${configuration}"

echo
echo "[3/4] Unit tests"
dotnet test "${repo_root}/Shalimar.slnx" --filter "Category!=Integration"

if [ "${skip_integration}" = "true" ]; then
  echo
  echo "[4/4] Skipping integration pipeline (--skip-integration)"
  exit 0
fi

echo
echo "[4/4] Integration pipeline (template -> IntegrationApp)"

artifacts="${repo_root}/artifacts"
runtime_ts="${repo_root}/src/Shalimar.Runtime/ts"
template_runtime="${repo_root}/src/Shalimar.Templates/templates/shalimar/Shared/runtime"
integration_app="${repo_root}/src/Shalimar.IntegrationApp"

echo "  Proving TS propagation (temporary marker)..."
marker="SHALIMAR_VERIFY_TS_PROPAGATION__$(date +%s)"
runtime_index="${runtime_ts}/src/index.ts"
runtime_index_bak="${runtime_index}.bak.verify"
cp "${runtime_index}" "${runtime_index_bak}"
cleanup_ts_marker() {
  if [ -f "${runtime_index_bak}" ]; then
    mv "${runtime_index_bak}" "${runtime_index}"
  fi
}
trap cleanup_ts_marker EXIT
printf "\n// %s\n" "${marker}" >> "${runtime_index}"

echo "  Packing ${version}..."
rm -rf "${artifacts}"
mkdir -p "${artifacts}"

rm -rf "${template_runtime}"
mkdir -p "${template_runtime}"
cp -R "${runtime_ts}/src/"* "${template_runtime}/"

# Proof: template runtime must now contain the marker (this is the packaging-time delivery mechanism).
grep -F "${marker}" "${template_runtime}/index.ts" >/dev/null || die "Template runtime did not include runtime TS marker"

dotnet build "${repo_root}/Shalimar.slnx" -c Release /p:Version="${version}"

projects=(
  "src/Shalimar.Runtime/Shalimar.Runtime.csproj"
  "src/Shalimar.SourceGenerator/Shalimar.SourceGenerator.csproj"
  "src/Shalimar.MSBuildTasks/Shalimar.MSBuildTasks.csproj"
  "src/Shalimar.RoslynAnalyzer/Shalimar.RoslynAnalyzer.csproj"
  "src/Shalimar.Vite/Shalimar.Vite.csproj"
  "src/Shalimar.Templates/Shalimar.Templates.csproj"
)

for p in "${projects[@]}"; do
  dotnet pack "${repo_root}/${p}" -c Release -o "${artifacts}" /p:Version="${version}" --no-build
done

echo "  Cleaning IntegrationApp..."
rm -rf "${integration_app}"

echo "  Clearing cached shalimar packages..."
rm -rf "${HOME}/.nuget/packages/shalimar"* 2>/dev/null || true

echo "  Installing template from local artifacts..."
dotnet new uninstall Shalimar.Templates >/dev/null 2>&1 || true
shopt -s nullglob
pkgs=( "${artifacts}"/Shalimar.Templates.*.nupkg )
shopt -u nullglob
[ ${#pkgs[@]} -gt 0 ] || die "Template package not found in ${artifacts}"
dotnet new install "${pkgs[0]}" --force

echo "  Creating IntegrationApp..."
(cd "${repo_root}/src" && dotnet new shalimar -n Shalimar.IntegrationApp)

echo "  Verifying generated app received runtime TS from template..."
grep -F "${marker}" "${integration_app}/Shared/runtime/index.ts" >/dev/null || die "Generated app is missing runtime TS marker under Shared/runtime (template delivery failed)"

cat > "${integration_app}/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="${artifacts}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

echo "  Building IntegrationApp..."
(cd "${integration_app}" && dotnet restore)
(cd "${integration_app}" && bun install)
(cd "${integration_app}" && dotnet build --no-restore)
(cd "${integration_app}" && bun run build)

echo "  Verifying build outputs..."
[ -f "${integration_app}/Generated/routeTree.gen.ts" ] || die "Missing Generated/routeTree.gen.ts (TanStack Router)"
[ -f "${integration_app}/wwwroot/dist/.vite/manifest.json" ] || die "Missing wwwroot/dist/.vite/manifest.json (Vite)"

if [ "${skip_e2e}" = "true" ]; then
  echo "  Skipping Playwright E2E (--skip-e2e)"
  exit 0
fi

echo "  Ensuring Playwright browsers are installed..."
dotnet build "${repo_root}/tests/Shalimar.IntegrationPlaywrightTests"
tools_dir="${repo_root}/.tools"
mkdir -p "${tools_dir}"
if [ ! -x "${tools_dir}/playwright" ]; then
  dotnet tool install --tool-path "${tools_dir}" Microsoft.Playwright.CLI --version 1.57.0
fi
${tools_dir}/playwright install chromium

echo "  Starting IntegrationApp on port ${port}..."
log_dir="${integration_app}/TestResults"
mkdir -p "${log_dir}"
app_log="${log_dir}/app.log"
shopt -s nullglob
app_projects=( "${integration_app}"/*.csproj )
shopt -u nullglob
[ ${#app_projects[@]} -gt 0 ] || die "No .csproj found under ${integration_app}"

ASPNETCORE_URLS="http://localhost:${port}" ASPNETCORE_ENVIRONMENT="Production" \
  dotnet run --project "${app_projects[0]}" --no-build >"${app_log}" 2>&1 &
app_pid=$!

cleanup() {
  if kill -0 "${app_pid}" >/dev/null 2>&1; then
    kill "${app_pid}" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "  Waiting for app to start..."
ready="false"
for _ in $(seq 1 30); do
  if curl -fsS "http://localhost:${port}/" >/dev/null 2>&1; then
    ready="true"
    break
  fi
  sleep 1
done

[ "${ready}" = "true" ] || die "IntegrationApp failed to start; see ${app_log}"

echo "  Running Playwright E2E tests..."
INTEGRATION_APP_URL="http://localhost:${port}" \
  dotnet test "${repo_root}/tests/Shalimar.IntegrationPlaywrightTests" --filter "Category=Integration"

echo
echo "✓ Verify complete"

