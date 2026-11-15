#!/usr/bin/env bash
# Runs dotnet-ef (pinned in dotnet-tools.json) inside the SDK container.
# Usage: ./scripts/ef.sh migrations add <Name> --output-dir Migrations
#        ./scripts/ef.sh migrations remove
#        ./scripts/ef.sh database update      (needs a reachable database, see below)
#
# To reach the compose database, run it on the compose network and pass the connection string:
#   EF_DOCKER_NETWORK=bankapp_default \
#   ConnectionStrings__BankAppDb="Server=sql;Database=BankApp;User Id=sa;Password=...;TrustServerCertificate=True" \
#   ./scripts/ef.sh database update
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE="${DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"

# The tool must be restored in the same container that runs it.
exec docker run --rm \
  --network "${EF_DOCKER_NETWORK:-bridge}" \
  ${ConnectionStrings__BankAppDb:+-e ConnectionStrings__BankAppDb} \
  -v "${REPO_ROOT}:/src" \
  -v bankapp-nuget-cache:/root/.nuget/packages \
  -w /src \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  "${IMAGE}" bash -c 'dotnet tool restore >/dev/null && dotnet ef "$@" --project src/Persistence --startup-project src/WebApi' _ "$@"
