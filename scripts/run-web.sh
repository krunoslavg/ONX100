#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$REPOSITORY_ROOT"

if ! command -v npm >/dev/null 2>&1; then
    echo "npm nije pronađen. Instalirajte Node.js i pokušajte ponovno." >&2
    exit 1
fi

echo "Pokrenite Onx100.Api u drugom terminalu prije korištenja frontenda."
echo "Pokretanje React development servera..."
echo

npm --prefix Onx100.Web run dev
