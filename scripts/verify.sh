#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if ! command -v pwsh >/dev/null 2>&1; then
  echo "Error: 'pwsh' (PowerShell 7+) is required." >&2
  echo "Install PowerShell: https://learn.microsoft.com/powershell/scripting/install/installing-powershell" >&2
  exit 1
fi

exec pwsh -NoLogo -NoProfile -File "${repo_root}/scripts/verify.ps1" "$@"

