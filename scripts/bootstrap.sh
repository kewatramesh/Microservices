#!/usr/bin/env bash
set -euo pipefail

for svc in services/*; do
  if [ -f "$svc/package.json" ]; then
    echo "Installing dependencies for $svc"
    (cd "$svc" && npm install)
  fi
done
