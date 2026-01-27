
# Sync-Upstream.ps1
# Synchronizes the upstream FModel repository and rebuilds FModelCLI.

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$UpstreamDir = Join-Path $ScriptDir "upstream\FModel"
$RepoUrl = "https://github.com/4sval/FModel.git"

Write-Host "=== FModelCLI Upstream Sync ===" -ForegroundColor Cyan

# 1. Check if upstream exists
if (-not (Test-Path $UpstreamDir)) {
    Write-Host "Clone new upstream from $RepoUrl..."
    New-Item -ItemType Directory -Path (Split-Path $UpstreamDir) -Force | Out-Null
    git clone --depth 1 $RepoUrl $UpstreamDir
} else {
    Write-Host "Updating existing upstream..."
    Push-Location $UpstreamDir
    try {
        git pull
    } finally {
        Pop-Location
    }
}

# 2. Restore Dependencies
Write-Host "Restoring dependencies..."
# We don't have an SLN file in this new setup yet, so let's try restoring project directly first.
# If that fails, we can add SLN creation logic here.
dotnet restore "$ScriptDir\FModelCLI\FModelCLI.csproj"

# 3. Build Release
Write-Host "Building Release..."
$DistDir = Join-Path $ScriptDir "dist"
dotnet publish "$ScriptDir\FModelCLI\FModelCLI.csproj" -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o $DistDir

Write-Host "Build complete! Output: $DistDir\FModelCLI.exe" -ForegroundColor Green
