#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$REPOSITORY_ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
    echo ".NET SDK nije pronađen. Instalirajte .NET 9 SDK i pokušajte ponovno." >&2
    exit 1
fi

echo "Korišteni .NET SDK: $(dotnet --version)"
echo "Repozitorij: $REPOSITORY_ROOT"

echo
echo "[1/3] Restore..."
dotnet restore Onx100.sln

echo
echo "[2/3] Release build..."
dotnet build Onx100.sln -c Release --no-restore

echo
echo "[3/3] Testovi..."
dotnet test Onx100.Driver.Tests/Onx100.Driver.Tests.csproj -c Release --no-build

echo
echo "Build i testovi uspješno završeni."
