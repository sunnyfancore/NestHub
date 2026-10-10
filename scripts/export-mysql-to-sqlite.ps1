param(
    [string]$ConfigFile = 'mysql-migration.env',
    [string]$OutputFile = 'backups/nesthub-export.db',
    [string]$Image = 'ghcr.io/sunnyfancore/nesthub:latest'
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw '请先安装并启动 Docker Desktop。' }
if (-not (Test-Path -LiteralPath $ConfigFile -PathType Leaf)) { throw '请复制 mysql-migration.env.example 为 mysql-migration.env，并填写数据库连接。' }
$migrationConfigPath = (Resolve-Path -LiteralPath $ConfigFile).Path
if ((Get-Content -LiteralPath $migrationConfigPath -Raw) -match 'your-mysql-host|your-password|your-user') { throw '请替换数据库配置中的示例地址和密码。' }
$migrationOutputPath = [System.IO.Path]::GetFullPath($OutputFile)
if (Test-Path -LiteralPath $migrationOutputPath) { throw "输出文件已存在，不会覆盖：$migrationOutputPath" }
$migrationOutputDirectory = [System.IO.Path]::GetDirectoryName($migrationOutputPath)
$migrationOutputName = [System.IO.Path]::GetFileName($migrationOutputPath)
New-Item -ItemType Directory -Path $migrationOutputDirectory -Force | Out-Null
docker pull $Image
if ($LASTEXITCODE -ne 0) { throw '镜像拉取失败。' }
docker run --rm --add-host host.docker.internal:host-gateway --env-file $migrationConfigPath -e "Migration__SqlitePath=/export/$migrationOutputName" --mount "type=bind,source=$migrationOutputDirectory,target=/export" $Image --migrate-mysql-to-sqlite
if ($LASTEXITCODE -ne 0) { throw '迁移失败，请检查数据库连接和导出日志。' }
Write-Host "SQLite 已导出：$migrationOutputPath"
Write-Host '原 MySQL 未修改。上传图片需另行复制。'
