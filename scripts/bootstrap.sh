#!/usr/bin/env bash
set -euo pipefail

for project in services/*/*.csproj; do
  echo "Restoring dependencies for $project"
  dotnet restore "$project"
done
