#!/usr/bin/env bash
# Runs the whole test suite inside the .NET 10 SDK container.
# Integration tests start a throwaway SQL Server container via Testcontainers, so the
# Docker socket is mounted into the SDK container.
# Usage: ./scripts/test.sh                      (whole solution)
#        ./scripts/test.sh --project <path>     (single test project)
#   Unit tests only (no Docker socket needed): ./scripts/dotnet.sh test --project tests/Application.UnitTests
set -euo pipefail

ARGS=("$@")
[ ${#ARGS[@]} -eq 0 ] && ARGS=(--solution BankApp.slnx)

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE="${DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"

exec docker run --rm \
  -v "${REPO_ROOT}:/src" \
  -v bankapp-nuget-cache:/root/.nuget/packages \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -w /src \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal \
  "${IMAGE}" dotnet test "${ARGS[@]}"
