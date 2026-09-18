$ErrorActionPreference = 'Stop'

$projectRoot = 'D:\Codex Work\BaiYunBox'
$dotnetRoot = 'D:\Codex Work\.tools\dotnet'
$iscc = 'D:\Codex\ISCC.exe'
$artifactsDir = Join-Path $projectRoot 'artifacts'
$publishDir = Join-Path $artifactsDir 'publish'

$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\dotnet-cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:PATH = "$dotnetRoot;$env:PATH"

# 关闭可能正在运行的实例
Get-Process -Name BaiYunBox -ErrorAction SilentlyContinue | Stop-Process -Force

# 清理旧发布目录
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

# 1) 构建（编译 + 单测前验证）
& "$dotnetRoot\dotnet.exe" build (Join-Path $projectRoot 'BaiYunBox.sln') -c Release `
    --logger 'console;verbosity=minimal'
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }

# 2) 自包含发布
& "$dotnetRoot\dotnet.exe" publish (Join-Path $projectRoot 'src\BaiYunBox\BaiYunBox.csproj') `
    -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

# 3) 复制许可证与说明文档
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $publishDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination $publishDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $publishDir -Force

# 4) Inno Setup 编译安装器
if (-not (Test-Path -LiteralPath $iscc)) {
    throw "ISCC.exe not found at $iscc"
}
& $iscc (Join-Path $projectRoot 'installer\baiyunbox.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup build failed with exit code $LASTEXITCODE." }

Write-Host "=== 构建完成 ===" -ForegroundColor Green
Get-ChildItem -LiteralPath $artifactsDir -Filter *.exe | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
    [pscustomobject]@{
        File = $_.FullName
        SizeMB = [math]::Round($_.Length / 1MB, 1)
        SHA256 = $hash.Hash
    }
} | Format-List
