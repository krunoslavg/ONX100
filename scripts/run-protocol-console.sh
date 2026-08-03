#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$REPOSITORY_ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
    echo ".NET SDK nije pronađen. Instalirajte .NET 9 SDK i pokušajte ponovno." >&2
    exit 1
fi

echo "Provjerite da ONX-100 simulator radi na 127.0.0.1:4999 i da drugi klijent nije spojen."
echo "Pokretanje Onx100.ProtocolConsole..."
echo

dotnet run --project Onx100.ProtocolConsole -c Release --no-build
