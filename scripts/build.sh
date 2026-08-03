#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
FRONTEND_DIRECTORY="$REPOSITORY_ROOT/Onx100.Web"
FRONTEND_DIST_DIRECTORY="$FRONTEND_DIRECTORY/dist"
API_WWWROOT_DIRECTORY="$REPOSITORY_ROOT/Onx100.Api/wwwroot"

cd "$REPOSITORY_ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
    echo ".NET SDK nije pronađen. Instalirajte .NET 9 SDK i pokušajte ponovno." >&2
    exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
    echo "npm nije pronađen. Instalirajte Node.js i pokušajte ponovno." >&2
    exit 1
fi

echo "Korišteni .NET SDK: $(dotnet --version)"
echo "Korišteni Node.js: $(node --version)"
echo "Korišteni npm: $(npm --version)"
echo "Repozitorij: $REPOSITORY_ROOT"

echo
echo "[1/6] Instalacija frontend ovisnosti..."
npm --prefix Onx100.Web ci

echo
echo "[2/6] Production build React frontenda..."
npm --prefix Onx100.Web run build

if [[ ! -d "$FRONTEND_DIST_DIRECTORY" ]]; then
    echo "Frontend build nije proizveo očekivani direktorij: $FRONTEND_DIST_DIRECTORY" >&2
    exit 1
fi

echo
echo "[3/6] Kopiranje frontenda u Onx100.Api/wwwroot..."
rm -rf "$API_WWWROOT_DIRECTORY"
mkdir -p "$API_WWWROOT_DIRECTORY"
cp -R "$FRONTEND_DIST_DIRECTORY"/. "$API_WWWROOT_DIRECTORY"/

echo
echo "[4/6] Restore .NET solutiona..."
dotnet restore Onx100.sln

echo
echo "[5/6] Release build .NET solutiona..."
dotnet build Onx100.sln -c Release --no-restore

echo
echo "[6/6] Testovi drivera..."
dotnet test Onx100.Driver.Tests/Onx100.Driver.Tests.csproj -c Release --no-build

echo
echo "Frontend, .NET build i testovi uspješno završeni."
