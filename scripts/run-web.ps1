$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $scriptDirectory "..")).Path

Set-Location $repositoryRoot

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm nije pronađen. Instalirajte Node.js i pokušajte ponovno."
}

Write-Host "Pokrenite Onx100.Api u drugom terminalu prije korištenja frontenda."
Write-Host "Pokretanje React development servera..."
Write-Host

npm --prefix Onx100.Web run dev
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
