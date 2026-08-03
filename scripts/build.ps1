$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $scriptDirectory "..")).Path

Set-Location $repositoryRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK nije pronađen. Instalirajte .NET 9 SDK i pokušajte ponovno."
}

Write-Host "Korišteni .NET SDK: $(dotnet --version)"
Write-Host "Repozitorij: $repositoryRoot"

Write-Host "`n[1/3] Restore..."
dotnet restore Onx100.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[2/3] Release build..."
dotnet build Onx100.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[3/3] Testovi..."
dotnet test Onx100.Driver.Tests/Onx100.Driver.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nBuild i testovi uspješno završeni."
