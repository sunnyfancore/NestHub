# Docker 部署与更新

镜像地址：`ghcr.io/sunnyfancore/nesthub`，支持 Linux AMD64 和 ARM64。

推送 `main` 后，GitHub Actions 自动构建前端、后端和最新插件压缩包，启动应用和 MySQL 做检查，再发布镜像。首次发布完成前不能拉取镜像。

## 一键部署（Linux 服务器）

安装 Docker Engine、Docker Compose v2（支持 `--wait`）、Git 和 OpenSSL，然后运行：

```bash
git clone https://github.com/sunnyfancore/NestHub.git
cd NestHub
bash scripts/deploy.sh
```

脚本自动生成 `.env` 和独立随机密码，创建持久化目录，拉取镜像并等待服务就绪。访问 `http://服务器IP:5186`，初始管理员为 `admin@example.com`，密码首次执行时显示并保存在 `.env`。登录后可以修改邮箱和密码。

再次运行脚本会保留 `.env` 和数据，并拉取更新。脚本需要当前用户能访问 Docker；设置上传目录权限时可能提示 sudo 密码。

如果第一次拉取出现 `denied`，在 GitHub 仓库右侧 **Packages → nesthub → Package settings → Change visibility → Public** 将镜像设为公开。GitHub 新建包可能默认私有；公开仓库不代表镜像自动公开。也可以用有 `read:packages` 权限的 GitHub 令牌登录 `ghcr.io` 拉取私有镜像。

## 手动 Compose 部署

只需要 `docker-compose.yml` 和 `.env`，运行镜像无需安装 .NET 或 Node.js，也无需复制 `publish` 目录。

```bash
cp .env.example .env
# 编辑 .env，替换所有 replace-with-... 示例，JWT_SECRET 至少 32 字符
mkdir -p data/app data/mysql backups
sudo chown -R 1654:1654 data/app
docker compose pull
docker compose up -d --wait --wait-timeout 300
```

数据库密码建议使用 `openssl rand -hex 24` 生成，避免连接字符串中的分号、引号以及 Compose 插值字符。更换端口修改 `.env` 中的 `NESTHUB_PORT`；通过反向代理访问时可设置 `NESTHUB_BIND_ADDRESS=127.0.0.1`。

## 挂载目录

所有相对路径均相对于 `docker-compose.yml` 所在目录：

| 宿主机 | 容器 | 保存内容 |
| --- | --- | --- |
| `./data/mysql` | MySQL 的 `/var/lib/mysql` | 账号、分类、书签、站点设置、SMTP 设置等数据库数据 |
| `./data/app` | NestHub 的 `/app/AppData` | 上传文件，网站 Logo 位于 `uploads/logos` |
| `./.env` | 由 Compose 读取，不挂载 | 数据库密码、JWT 密钥、管理员初始设置、镜像和端口 |
| `./backups` | 不自动挂载 | 自己保存的数据库导出与上传文件备份 |

不要再挂载 `./publish:/app` 或整个 `/app`，否则会覆盖镜像中的程序、主题和插件，导致更新不生效。也不要挂载整个 `wwwroot`；前端和插件已经在镜像里。

自定义数据盘可把 Compose 的卷源改成绝对路径，例如 `/srv/nesthub/app:/app/AppData` 和 `/srv/nesthub/mysql:/var/lib/mysql`。上传目录需允许 UID `1654` 写入；MySQL 的目录由官方镜像初始化管理。`.env` 和数据目录已被 Git 与 Docker 构建上下文排除。

## 更新

先备份，然后在部署目录运行：

```bash
docker compose pull nesthub
docker compose up -d --no-deps --wait --wait-timeout 300 nesthub
```

这会更新程序和插件，保留数据库及上传文件，不升级 MySQL。服务重建时有短暂中断。后端自动同步表结构，因此升级前应备份数据；旧镜像不保证兼容升级后的数据库结构。

如果需要同步 Compose 配置或脚本，也可以 `git pull --ff-only` 后重新运行部署脚本。保留本地 `.env`，数据库已有数据时，修改 `.env` 的 MySQL 密码不会自动修改数据库用户密码。管理员邮箱/密码配置也只在首次初始化时生效；JWT 密钥变更会使现有登录令牌失效。

## 镜像标签与回退

- `latest`：最近一次 `main` 构建成功的镜像。
- `sha-xxxxxxx`：按 Git 提交固定的镜像，适合可重复部署。
- `1.3.0`、`1.3`：推送 `v1.3.0` 这样的 Git 标签后自动生成版本镜像。

在 `.env` 中把 `NESTHUB_IMAGE` 改为目标版本，再执行更新命令。`latest` 只随 `main` 更新，历史标签重新构建不会覆盖它。

## 备份与恢复

在 Linux 部署目录运行，保留 `.env`，停止应用以避免备份过程中的写入：

```bash
mkdir -p backups
docker compose stop nesthub
docker compose exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_PASSWORD" mysqldump -u nesthub --single-transaction --no-tablespaces nesthub' > backups/nesthub.sql
tar -czf backups/app-data.tar.gz -C data app
docker compose start nesthub
```

恢复到配置好相同密码的部署环境中：

```bash
docker compose up -d --wait mysql
docker compose stop nesthub
docker compose exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_PASSWORD" mysql -u nesthub nesthub' < backups/nesthub.sql
tar -xzf backups/app-data.tar.gz -C data
sudo chown -R 1654:1654 data/app
docker compose up -d --wait --wait-timeout 300 nesthub
```

备份命令失败时不要继续恢复；数据库目录不能在 MySQL 运行时直接复制作为可靠备份。不要删除 `data` 目录。

## 原有部署迁移

原 Compose 使用外部 MySQL，新 Compose 默认新增内置 MySQL，不能直接把空数据库当作原站点。迁移前先从原数据库导出 SQL，再导入新 MySQL，并把旧 `publish/AppData` 中的上传文件复制到新 `data/app`，最后检查用户、分类、书签和 Logo。

继续使用外部 MySQL时：移除 Compose 的 `mysql` 服务及 `nesthub.depends_on`，将 `Database__ConnectionString` 改为自己的外部数据库连接。容器中的 `localhost` 指向容器自身，不能用它连接宿主机数据库。保留原 JWT 密钥可避免现有令牌因签名变化失效。

## 构建和排查

本地从源码构建（无需手动发布）：

```bash
docker build -t nesthub:local .
# .env 设置 NESTHUB_IMAGE=nesthub:local
docker compose up -d --wait --wait-timeout 300
```

```bash
docker compose ps
docker compose logs --tail=100 nesthub mysql
curl -f http://localhost:5186/health
```

自动构建状态见仓库 **Actions → Docker image**。工作流使用 GitHub 自带 `GITHUB_TOKEN` 写入 GHCR，无需配置 Docker Hub 账号。若提示写包权限不足，检查 Actions 是否启用、组织包策略及现有包的仓库访问权限。
