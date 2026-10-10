#!/usr/bin/env bash
set -euo pipefail
# Usage: bash scripts/export-mysql-to-sqlite.sh [config-file] [output-file]
# Paths supplied by the user are relative to the current directory.
config_file="${1:-mysql-migration.env}"
output_file="${2:-backups/nesthub-export.db}"
image="${NESTHUB_IMAGE:-ghcr.io/sunnyfancore/nesthub:latest}"
command -v docker >/dev/null || { echo '请先安装 Docker。此脚本不需要本机安装 .NET 或 MySQL 客户端。'; exit 1; }
if [ ! -f "$config_file" ]; then
  echo "请复制 mysql-migration.env.example 为 $config_file，并填写 MySQL 连接字符串。"
  exit 1
fi
if grep -q 'your-mysql-host\|your-password\|your-user' "$config_file"; then
  echo '请先替换配置文件中的 MySQL 地址、用户名和密码。'; exit 1
fi
if [ -e "$output_file" ]; then
  echo "输出文件已存在，不会覆盖：$output_file"; exit 1
fi
mkdir -p "$(dirname "$output_file")"
output_dir="$(cd "$(dirname "$output_file")" && pwd)"
output_name="$(basename "$output_file")"
chmod 600 "$config_file"
docker pull "$image"
docker run --rm \
  --user "$(id -u):$(id -g)" \
  --add-host host.docker.internal:host-gateway \
  --env-file "$config_file" \
  -e "Migration__SqlitePath=/export/$output_name" \
  --mount "type=bind,source=$output_dir,target=/export" \
  "$image" --migrate-mysql-to-sqlite
echo "已导出 SQLite：$output_dir/$output_name"
echo '账号、设置和书签已保留；上传图片需另行复制。'
