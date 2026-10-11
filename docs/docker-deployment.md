# Docker 部署、更新与数据库迁移

镜像：`ghcr.io/sunnyfancore/nesthub`，支持 Linux AMD64 / ARM64，可公开拉取。推送 `main` 后自动构建网站、后端和插件，测试 SQLite、MySQL、数据迁移及持久化后发布镜像。

## SQLite 一键部署

Linux 服务器安装 Docker Engine、Docker Compose v2、Git、OpenSSL 后运行：

```bash
git clone https://github.com/sunnyfancore/NestHub.git
cd NestHub
bash scripts/deploy.sh
```

默认使用 SQLite，仅运行一个 NestHub 容器，内置后台更新逻辑，无需 Watchtower 或数据库容器。脚本生成 `.env` 和随机密码，设置挂载目录权限并启动应用。访问 `http://服务器IP:5186`，初始邮箱 `admin@example.com`，密码首次显示并保存在 `.env`。设置目录权限时可能提示 sudo 密码。

手动部署：

```bash
cp .env.example .env
# 编辑 .env，替换示例密码，JWT_SECRET 至少 32 字符
mkdir -p data/app backups
sudo chown -R 1654:1654 data/app
docker compose pull
docker compose up -d --wait --wait-timeout 300
```

SQLite 适合个人、小团队的单实例部署，不要让多个实例共享同一个数据库文件；大量并发写入建议继续使用 MySQL。端口通过 `.env` 的 `NESTHUB_PORT` 修改；仅通过本机反向代理访问可设置 `NESTHUB_BIND_ADDRESS=127.0.0.1`。

## MySQL 部署

使用独立配置，不要与默认 SQLite Compose 同时启动同一项目：

```bash
cp .env.example .env
# 编辑 .env，替换全部示例密码；数据库密码可用 openssl rand -hex 24 生成
mkdir -p data/app data/mysql backups
sudo chown -R 1654:1654 data/app
docker compose -f docker-compose.mysql.yml pull
docker compose -f docker-compose.mysql.yml up -d --wait --wait-timeout 300
```

管理 MySQL 版本时都要加 `-f docker-compose.mysql.yml`。原配置未指定 `Database:Provider` 时仍使用 MySQL，不会自动迁移数据。

## 目录挂载

相对路径以 Compose 文件所在目录为准。

| 宿主机 | 容器 | 内容 |
| --- | --- | --- |
| `./data/app` | `/app/AppData` | SQLite 的 `nesthub.db`、WAL/SHM 文件和上传目录 `uploads/` |
| `./data/mysql` | MySQL `/var/lib/mysql` | 仅 MySQL 版使用的数据库数据 |
| `./.env` | Compose 读取，不挂载 | JWT 密钥、密码、端口、镜像等 |
| `./backups` | 不自动挂载 | 导出的数据库及文件备份 |

不要挂载整个 `/app` 或 `wwwroot`，否则覆盖镜像里的程序，更新不生效。可把数据目录改为 `/srv/nesthub/app:/app/AppData` 等绝对路径，目录必须允许 UID `1654` 写入。`.env`、数据库、上传文件和备份不进入 Git 或 Docker 构建。

## 镜像内置 MySQL → SQLite 迁移

只需要 Docker 和原 MySQL 连接，无需部署新应用或本机安装 .NET。

```bash
touch mysql-migration.env
chmod 600 mysql-migration.env
```

在 `mysql-migration.env` 中填写这一行（不用额外引号）：

```dotenv
Migration__MySqlConnectionString=Server=数据库地址;Port=3306;Database=nesthub;User ID=用户名;Password=密码;Charset=utf8mb4;SslMode=None;
```

运行：

```bash
mkdir -p backups
sudo chown 1654:1654 backups
docker run --rm --add-host host.docker.internal:host-gateway \
  --env-file mysql-migration.env \
  -e Migration__SqlitePath=/export/nesthub-export.db \
  --mount "type=bind,source=$PWD/backups,target=/export" \
  ghcr.io/sunnyfancore/nesthub:latest --migrate-mysql-to-sqlite
```

默认输出 `backups/nesthub-export.db`。导出覆盖八张表：账号和密码哈希、站点设置、分类、书签、分享链接、跳转设置、密码重置记录、SMTP 设置。保留 ID、排序、点击数、图标等字段，逐表逐字段校验。原数据库使用只读事务，不会被修改；先写临时文件，成功后才生成正式文件；输出文件已存在时拒绝覆盖。命令失败则不要切换生产站。

数据库地址必须能从容器访问。宿主机 MySQL 可以使用 `host.docker.internal`（命令配置了 host-gateway），前提是 MySQL 允许该连接；其他数据库填写可达 IP/域名。容器内 `localhost` 不是宿主机。同一 Docker 网络内的 MySQL 容器可将 `docker run` 增加 `--network 网络名`，使用 MySQL 容器名连接。配置文件包含密码，已从 Git 和镜像构建排除。

## 用导出的 SQLite 接管旧站

1. 先备份原 MySQL 和上传文件，暂停旧 NestHub 的写入，保持 MySQL 运行，再执行迁移命令。不要删除原库。
2. 准备新部署目录的 `.env`，保留原 JWT 密钥；原管理员账号和密码会迁移，初始管理员配置不会重置已有账号。
3. 在新应用首次启动前，把导出的文件复制为 `data/app/nesthub.db`，同时复制旧 `AppData/uploads` 到 `data/app/uploads`。不要覆盖已使用的新库。
4. 执行 `sudo chown -R 1654:1654 data/app`，然后 `docker compose up -d --wait --wait-timeout 300`。
5. 用原账号登录，核对用户、分类、书签、公共导航、分享链接、Logo 和设置，再切换域名或反向代理。

上传图片不在数据库中，必须另行复制。MySQL 的 SQL 文件不能直接导入 SQLite；旧后台 JSON 导出也不是整站恢复文件。切换后的 SQLite 修改不会自动回写原 MySQL，保留旧库和备份以便回退。

## 更新与固定版本

### 后台一键更新（默认包含）

SQLite 部署只运行一个 NestHub 容器；MySQL 版另有数据库容器。日常更新下载应用包，在当前容器内部重启程序，不再拉取镜像或替换容器。

```yaml
environment:
  Updates__Enabled: "true"
  # 可选：访问 GitHub 的 HTTP 或 SOCKS5 代理
  # Updates__ProxyUrl: http://192.168.1.10:7890
volumes:
  - ./data/app:/app/AppData
```

完整示例见根目录 `docker-compose.yml` 和 `docker-compose.mysql.yml`。无需 Docker socket、Watchtower、root 用户或更新令牌。应用包适配 Linux x64 / ARM64 与 .NET 10；基础运行环境的大版本升级仍需要更新 Docker 镜像。

更新过程：查询 GitHub Releases → 下载当前架构的应用包 → SHA-256 和大小校验 → 解压到独立版本目录 → 重启应用 → 健康检查。下载期间网站可用，重启时短暂断开。新程序启动失败自动恢复旧程序；数据库结构变更不能自动撤销，因此更新前仍须备份。

首次切换：保留 `.env` 与原有数据目录，移除旧 `updater` 服务、Docker socket 挂载、`user: "0:0"` 和 `Updates__ContainerName`。旧版写入的文件可能属于 root，先调整权限，再更新一次基础镜像：

```bash
sudo chown -R 1654:1654 ./data/app
docker compose pull nesthub
docker compose up -d --remove-orphans --wait --wait-timeout 300
```

之后超级管理员进入 **后台 → 应用更新** 操作。MySQL 版添加 `-f docker-compose.mysql.yml`。修改 Compose、环境变量或基础镜像后仍需重建容器。

持久目录：

| 路径 | 用途 |
| --- | --- |
| `AppData/nesthub.db` | SQLite 数据库 |
| `AppData/uploads` | 上传文件 |
| `AppData/updates/releases/<提交号>` | 独立应用版本（包括前端和插件包） |
| `AppData/updates/active` | 当前成功启用版本 |
| `AppData/updates/previous` | 上一个版本 |
| `AppData/updates/pending.json` | 等待启动验证的版本 |
| `AppData/updates/status.json` | 更新进度与结果 |

应用版本随数据目录保存，普通容器重建不会丢失。手动更新镜像不会覆盖已经启用的应用包。若需恢复使用镜像内置程序，先停止容器、备份数据库和 `updates` 目录，再移走 `updates/active` 与 `updates/pending.json`，启动容器。不要删除数据库或上传目录；旧程序未必兼容新版数据库结构。

GitHub 应用包和 Docker 镜像是两个独立下载通道。`ghcr.dockerproxy.net` 只用于镜像；服务器访问 GitHub 较慢时配置 `Updates__ProxyUrl`。代理应能访问 GitHub API、Release 附件及其文件 CDN。发布流水线仅在完整上传所有包和清单后公开应用版本，未完成的草稿不会被检测为更新。

### 手动更新

先备份，再执行：

```bash
docker compose pull nesthub
docker compose up -d --no-deps --wait --wait-timeout 300 nesthub
```

MySQL 版两条命令都加 `-f docker-compose.mysql.yml`。数据库和上传文件保留，MySQL 容器不更新。重建时有短暂中断；程序自动同步表结构，旧版本不保证兼容更新后的结构。

- `latest`：最近一次 `main` 成功构建。
- `sha-xxxxxxx`：固定 Git 提交。
- `1.3.1` / `1.3`：推送 `v1.3.1` 等 Git 标签后生成。

修改 `.env` 的 `NESTHUB_IMAGE` 可固定版本，再执行更新命令。标签构建不覆盖 `latest`。MySQL 已有数据时，修改环境密码不会自动修改数据库密码；JWT 密钥变更会使旧令牌失效。

## 备份和恢复

SQLite：停止应用后备份整个 AppData，包括数据库、WAL 和上传文件。

```bash
mkdir -p backups
docker compose stop nesthub
tar -czf backups/app-data.tar.gz -C data app
docker compose start nesthub
```

恢复时先停止应用，保留现有 `data/app` 的副本，将备份解压到 `data`，恢复 UID `1654` 权限后启动。不要在应用运行时只复制 `nesthub.db`，可能遗漏 WAL 中的新数据。

MySQL：

```bash
mkdir -p backups
docker compose -f docker-compose.mysql.yml stop nesthub
docker compose -f docker-compose.mysql.yml exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_PASSWORD" mysqldump -u nesthub --single-transaction --no-tablespaces nesthub' > backups/nesthub.sql
tar -czf backups/app-data.tar.gz -C data app
docker compose -f docker-compose.mysql.yml start nesthub
```

恢复时应用保持停止、MySQL 启动，运行：

```bash
docker compose -f docker-compose.mysql.yml exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_PASSWORD" mysql -u nesthub nesthub' < backups/nesthub.sql
```

再恢复上传文件。保留 `.env`；不要在 MySQL 运行时直接复制其数据库目录作为可靠备份。

## 外部数据库与源码配置

继续使用外部 MySQL：在 MySQL Compose 中删除 `mysql` 服务和 `depends_on`，将 `Database__ConnectionString` 改为原连接地址。不要把内置空数据库当作原库。

源码使用 SQLite：

```json
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=AppData/nesthub.db",
    "AutoSyncStructure": true
  }
}
```

相对路径以应用内容根目录为准，父目录自动创建。SQLite 使用 WAL 和 30 秒锁等待。MySQL 使用 `Provider: MySql` 和原连接字符串。

## 排查

```bash
docker compose ps
docker compose logs --tail=100 nesthub
curl -f http://localhost:5186/health
```

源码构建：`docker build -t nesthub:local .`，把 `.env` 的 `NESTHUB_IMAGE` 改为 `nesthub:local` 后启动。无需手动生成 publish 目录。

自动构建见 **Actions → Docker image**，使用 GitHub 自带 `GITHUB_TOKEN`，无需 Docker Hub 账号。Fork 后镜像拉取报 denied 时，可将 GitHub Packages 的镜像包设为 Public 或登录 GHCR 拉取私有镜像。
