#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "This helper is only needed on Linux."
  exit 1
fi

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
declare -a candidates=(
  "$project_root/node_modules/electron/dist/chrome-sandbox"
  "$project_root/dist/linux-unpacked/chrome-sandbox"
)
declare -a targets=()

for candidate in "${candidates[@]}"; do
  if [[ -f "$candidate" && ! -L "$candidate" ]]; then
    case "$candidate" in
      "$project_root"/*) targets+=("$candidate") ;;
    esac
  fi
done

if [[ ${#targets[@]} -eq 0 ]]; then
  echo "No Electron chrome-sandbox helper was found. Run npm install or build first."
  exit 1
fi

echo "The following Electron helpers will become root-owned with mode 4755:"
printf '  %s\n' "${targets[@]}"
sudo chown root:root -- "${targets[@]}"
sudo chmod 4755 -- "${targets[@]}"

echo "Electron sandbox helpers configured. You can now run npm start or dist/linux-unpacked/codex-usage-widget."
