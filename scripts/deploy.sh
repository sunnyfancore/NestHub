#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
command -v docker >/dev/null || { echo '请先安装 Docker Engine 和 Docker Compose。'; exit 1; }
docker compose version >/dev/null
if [ ! -f .env ]; then
  command -v openssl >/dev/null || { echo '请先安装 openssl，或参照 .env.example 手动创建 .env。'; exit 1; }
  umask 077
  admin_password="$(openssl rand -hex 16)"
  cat > .env <<EOF
NESTHUB_IMAGE=ghcr.io/sunnyfancore/nesthub:latest
NESTHUB_BIND_ADDRESS=0.0.0.0
NESTHUB_PORT=5186
TZ=Asia/Shanghai
ADMIN_EMAIL=admin@example.com
ADMIN_PASSWORD=$admin_password
JWT_SECRET=$(openssl rand -hex 32)
MYSQL_PASSWORD=$(openssl rand -hex 24)
MYSQL_ROOT_PASSWORD=$(openssl rand -hex 24)
EOF
  echo '已生成独立密码并保存到 .env（请妥善保管）。'
  echo '初始管理员：admin@example.com'
  echo "初始密码：$admin_password"
fi
if grep -q 'replace-with-' .env; then
  echo '请先替换 .env 中的示例密码。'; exit 1
fi
mkdir -p data/app backups
# Keep data writable for the default .NET app user, including when native updates are disabled.
if [ "$(id -u)" = 0 ]; then
  chown -R 1654:1654 data/app
elif command -v sudo >/dev/null; then
  sudo chown -R 1654:1654 data/app
else
  echo '请执行 chown -R 1654:1654 data/app 后重试，或使用 sudo bash scripts/deploy.sh。'; exit 1
fi
docker compose config --quiet
docker compose pull
docker compose up -d --wait --wait-timeout 300
echo '部署完成。使用服务器地址和 .env 中的 NESTHUB_PORT 访问（默认端口 5186）。'
