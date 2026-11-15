#!/usr/bin/env bash
# Runs the .NET CLI inside the official .NET 10 SDK container, so no local SDK is required.
# Usage: ./scripts/dotnet.sh <dotnet arguments>   e.g. ./scripts/dotnet.sh build
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE="${DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"

TTY_FLAGS=()
if [ -t 0 ] && [ -t 1 ]; then
  TTY_FLAGS=(-it)
fi

exec docker run --rm ${TTY_FLAGS[@]+"${TTY_FLAGS[@]}"} \
  -v "${REPO_ROOT}:/src" \
  -v bankapp-nuget-cache:/root/.nuget/packages \
  -w /src \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  "${IMAGE}" dotnet "$@"
