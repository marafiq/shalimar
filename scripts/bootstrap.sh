#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
sdk_version="$(rg -n '\"version\"\\s*:\\s*\"([^\"]+)\"' "${repo_root}/global.json" -r '$1' | head -1)"

die() { echo "Error: $*" >&2; exit 1; }
need_cmd() { command -v "$1" >/dev/null 2>&1 || die "Missing prerequisite '$1' ($2)"; }

need_cmd curl "required to download installers"

echo "Bootstrapping Shalimar prerequisites..."
echo "  Repo: ${repo_root}"
echo "  .NET SDK: ${sdk_version:-<unknown>}"

if ! command -v dotnet >/dev/null 2>&1; then
  [ -n "${sdk_version:-}" ] || die "Could not detect SDK version from global.json"
  echo
  echo "Installing dotnet SDK ${sdk_version} to ~/.dotnet ..."
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --version "${sdk_version}" --install-dir "${HOME}/.dotnet" --no-path
  echo "Installed dotnet to ${HOME}/.dotnet"
else
  echo
  echo "dotnet already installed: $(dotnet --version)"
fi

if ! command -v bun >/dev/null 2>&1; then
  echo
  echo "Installing bun to ~/.bun ..."
  curl -fsSL https://bun.sh/install | bash
  echo "Installed bun to ${HOME}/.bun/bin"
else
  echo
  echo "bun already installed: $(bun --version)"
fi

echo
echo "Next:"
echo "  export PATH=\"${HOME}/.dotnet:${HOME}/.bun/bin:\$PATH\""
echo "  bash ./scripts/verify.sh"

