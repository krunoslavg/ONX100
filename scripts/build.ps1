$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $scriptDirectory "..")).Path
$frontendDirectory = Join-Path $repositoryRoot "Onx100.Web"
$frontendDistDirectory = Join-Path $frontendDirectory "dist"
$apiWwwRootDirectory = Join-Path $repositoryRoot "Onx100.Api\wwwroot"

Set-Location $repositoryRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK nije pronađen. Instalirajte .NET 9 SDK i pokušajte ponovno."
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm nije pronađen. Instalirajte Node.js i pokušajte ponovno."
}

if (-not (Get-Command robocopy -ErrorAction SilentlyContinue)) {
    throw "Robocopy nije pronađen. Skriptu pokrenite na podržanoj Windows instalaciji."
}

Write-Host "Korišteni .NET SDK: $(dotnet --version)"
Write-Host "Korišteni Node.js: $(node --version)"
Write-Host "Korišteni npm: $(npm --version)"
Write-Host "Repozitorij: $repositoryRoot"

Write-Host "`n[1/6] Instalacija frontend ovisnosti..."
npm --prefix Onx100.Web ci
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[2/6] Production build React frontenda..."
npm --prefix Onx100.Web run build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path $frontendDistDirectory)) {
    throw "Frontend build nije proizveo očekivani direktorij: $frontendDistDirectory"
}

Write-Host "`n[3/6] Kopiranje frontenda u Onx100.Api/wwwroot..."
robocopy $frontendDistDirectory $apiWwwRootDirectory /MIR /NFL /NDL /NJH /NJS /NP
$robocopyExitCode = $LASTEXITCODE
if ($robocopyExitCode -ge 8) { exit $robocopyExitCode }

Write-Host "`n[4/6] Restore .NET solutiona..."
dotnet restore Onx100.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[5/6] Release build .NET solutiona..."
dotnet build Onx100.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[6/6] Testovi drivera..."
dotnet test Onx100.Driver.Tests/Onx100.Driver.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nFrontend, .NET build i testovi uspješno završeni."
