$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $scriptDirectory "..")).Path

Set-Location $repositoryRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK nije pronađen. Instalirajte .NET 9 SDK i pokušajte ponovno."
}

Write-Host "Provjerite da ONX-100 simulator radi na 127.0.0.1:4999 i da drugi klijent nije spojen."
Write-Host "Pokretanje Onx100.ProtocolConsole...`n"

dotnet run --project Onx100.ProtocolConsole -c Release --no-build
exit $LASTEXITCODE
