param(
    [Parameter(Mandatory=$false)]
    [switch]$Update = $false,   # 用户明确要求更新到最新上游
    [switch]$SkipGit = $false   # CI 中跳过所有 Git 操作
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$UpstreamDir = Join-Path $ScriptDir "upstream\FModel"

Write-Host "=== FModelCLI Build System ===" -ForegroundColor Cyan

# 1. 智能 Git 逻辑
if (-not $SkipGit) {
    # 检查是否在 Git 仓库中
    $isGitRepo = Test-Path (Join-Path $ScriptDir ".git")

    if ($isGitRepo) {
        if ($Update) {
            Write-Host "Updating submodules to latest upstream..." -ForegroundColor Yellow
            git submodule update --remote --init --recursive
        } else {
            # 只有在目录为空时才初始化，避免覆盖用户手动切换的版本
            $FModelFiles = Get-ChildItem $UpstreamDir -ErrorAction SilentlyContinue
            if (-not $FModelFiles) {
                Write-Host "Initializing submodules..."
                git submodule update --init --recursive
            } else {
                Write-Host "Submodule already initialized. Skipping update to preserve current version." -ForegroundColor Gray
            }
        }
    } else {
        # 如果不是 Git 仓库（比如下载的 Zip 源码），则走原始 Clone 逻辑
        if (-not (Test-Path $UpstreamDir)) {
            Write-Host "Not a git repo, cloning upstream..."
            git clone --depth 1 "https://github.com/4sval/FModel.git" $UpstreamDir
        }
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
