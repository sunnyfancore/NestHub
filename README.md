# NestHub

把常用网站收进自己的导航主页。NestHub 支持独立用户空间、分类书签、浏览器快捷收藏和多种中文主题，可部署在自己的服务器上。

[Docker 部署与迁移指南](docs/docker-deployment.md) · [浏览器插件说明](extensions/nesthub/README.md) · [镜像构建记录](https://github.com/sunnyfancore/NestHub/actions/workflows/docker.yml)

## Docker Compose 部署（推荐）

镜像：`ghcr.io/sunnyfancore/nesthub:latest`，支持 Linux AMD64 / ARM64。默认使用 SQLite，只运行一个 NestHub 容器并内置后台更新逻辑，无需安装 MySQL、Node.js 或 .NET。

### 1. 准备配置

在服务器新建一个部署目录，例如 `/srv/nesthub`。将下面内容保存为该目录中的 `docker-compose.yml`，不需要克隆整个仓库：

```yaml
name: nesthub
services:
  nesthub:
    container_name: nesthub
    image: ${NESTHUB_IMAGE:-ghcr.io/sunnyfancore/nesthub:latest}
    ports:
      - "${NESTHUB_BIND_ADDRESS:-0.0.0.0}:${NESTHUB_PORT:-5186}:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      TZ: ${TZ:-Asia/Shanghai}
      Database__Provider: Sqlite
      Database__ConnectionString: "Data Source=/app/AppData/nesthub.db"
      Database__AutoSyncStructure: "true"
      Jwt__SecretKey: ${JWT_SECRET:?Configure JWT_SECRET in .env}
      SuperAdmin__Email: ${ADMIN_EMAIL:-admin@example.com}
      SuperAdmin__Password: ${ADMIN_PASSWORD:?Configure ADMIN_PASSWORD in .env}
      SuperAdmin__DisplayName: 超级管理员
      Updates__Enabled: "true"
    volumes:
      - ./data/app:/app/AppData
    restart: unless-stopped

```

在同一目录新建 `.env`，填写以下内容，把示例值换成自己的密码与随机密钥：

```dotenv
ADMIN_EMAIL=admin@example.com
ADMIN_PASSWORD=请替换为自己的强密码
JWT_SECRET=请替换为至少32字符的随机密钥
```

Linux 可用 `openssl rand -hex 32` 生成密钥。已有站点迁移时保留原 JWT 密钥；管理员配置用于初次创建账号，不会重置已有账号密码。

### 2. 创建挂载目录并启动

需要 Docker Engine 和 Docker Compose v2。在上述部署目录运行：

```bash
mkdir -p data/app
sudo chown -R 1654:1654 data/app
docker compose up -d --wait --wait-timeout 300
```

访问 `http://服务器IP:5186`，使用 `.env` 中的邮箱和密码登录。首次启动自动创建数据库与表结构。对外使用域名时，建议通过反向代理配置 HTTPS。

想让脚本自动生成配置和密码，可使用仓库内的一键部署方式（Linux，需 Git、OpenSSL）：

```bash
git clone https://github.com/sunnyfancore/NestHub.git
cd NestHub
bash scripts/deploy.sh
```

### 3. 目录挂载说明

`./data/app` 指 **Compose 文件所在目录** 下的 `data/app`，不是容器内部目录。整个目录需要保留。

```text
/srv/nesthub/
├── docker-compose.yml
├── .env                       # 管理员配置与 JWT 密钥
├── data/
│   └── app/
│       ├── nesthub.db          # SQLite 数据库
│       ├── nesthub.db-wal      # 运行时可能存在，备份时一并保留
│       ├── nesthub.db-shm      # 运行时可能存在
│       └── uploads/           # 上传的图片、Logo 等
└── backups/                   # 可自行创建，用于备份
```

| 宿主机位置 | 容器位置 | 用途 |
| --- | --- | --- |
| `./data/app` | `/app/AppData` | SQLite 数据库与上传文件，必须持久化 |
| `./data/mysql` | `/var/lib/mysql` | 仅 MySQL 部署使用 |
| `.env` | 不挂载，由 Compose 读取 | 密钥、密码等配置 |
| `./backups` | 默认不挂载 | 自己保存的备份文件 |

也可以改用绝对路径，例如将 `volumes` 改成：

```yaml
    volumes:
      - /srv/nesthub-data:/app/AppData
```

这时应创建 `/srv/nesthub-data`，并赋予 UID `1654` 写入权限。NAS 也可以使用自己的共享目录路径。不要挂载整个 `/app` 或 `/app/wwwroot`，否则会覆盖镜像中的程序，导致更新无效。

SQLite 适合个人和小团队的单实例部署；大量并发写入可选择 MySQL。MySQL 使用仓库中的独立配置 [docker-compose.mysql.yml](docker-compose.mysql.yml)，具体启动、外部数据库连接和迁移方法见 [部署指南](docs/docker-deployment.md)。切换数据库类型不会自动搬迁原数据。

## 更新与备份

### 手动更新镜像

在部署目录运行，数据库和上传文件继续保留在挂载目录中：

```bash
docker compose pull nesthub
docker compose up -d --no-deps --wait --wait-timeout 300 nesthub
```

更新前备份 `.env` 和整个 `data/app`。SQLite 文件备份应先停止应用，复制完整目录后再启动，避免只复制正在写入的 `nesthub.db`。MySQL 备份与恢复、版本固定和回退见 [完整指南](docs/docker-deployment.md)。后台书签导出适合搬运书签，不等于整站备份。

### 后台点击更新（默认可用）

后台从 GitHub Releases 下载应用包，校验后在同一容器内切换程序并重启。无需 Docker socket、Watchtower 或额外更新容器。新版本启动失败时自动恢复旧程序；数据库结构不自动回滚，更新前请备份。

首次切换需要手动拉取一次新基础镜像并重建容器，移除旧 socket 挂载、`user: "0:0"` 和 `Updates__ContainerName`。数据目录须允许 UID 1654 写入。之后进入 **后台 → 应用更新**，可查看下载、校验、重启与健康检查进度。

更新包默认直接从 GitHub 下载，可用 `Updates__ProxyUrl` 配置 HTTP / SOCKS5 代理。Docker 镜像代理只用于镜像，不会加速 GitHub 应用包。应用版本保存在 `AppData/updates`，重建容器会继续使用已更新版本；运行环境升级仍需更新基础镜像。完整说明见 [部署指南](docs/docker-deployment.md)。

## 浏览器快捷收藏

支持 Chrome、Edge、Brave 等 Chromium 浏览器。登录后台进入 **获取 API**，下载插件压缩包并查看安装指引。

1. 解压压缩包，在浏览器扩展管理页开启“开发者模式”。
2. 点击“加载已解压的扩展程序”，选择包含 `manifest.json` 的目录。
3. 在插件中填写 NestHub 站点地址，使用 API 令牌连接；也可按插件提示读取同一浏览器中已登录站点的令牌。
4. 打开任意网页，通过插件或贴边收藏按钮选择分类并保存。

插件提供分类缓存、强制刷新、主题切换、可拖动的贴边按钮，以及可选的浏览器启动时打开导航主页。`Alt+Shift+B` 若未生效，可在浏览器扩展快捷键设置中重新绑定。远程站点请使用 HTTPS。

每次构建发布网站和 Docker 镜像，都会自动打包最新插件供后台下载。**已安装的手动版插件需要自行下载新版并重新加载**；Chrome 商店发布是独立流程。详细操作见 [插件说明](extensions/nesthub/README.md)。

## 功能与主题

| 功能 | 说明 |
| --- | --- |
| 独立空间 | 多租户数据隔离，支持个人导航与公共展示 |
| 书签管理 | 分类、拖拽排序、置顶、标签、浏览器 HTML 导入 |
| 搜索导航 | 切换搜索引擎、搜索建议，支持单个字符查询 |
| 网站图标 | 自动识别网站图标并缓存，支持手动调整 |
| 分享与导出 | 分享链接、书签 JSON 与浏览器 HTML 导出 |
| 管理后台 | 用户与租户、分类、链接、主题、SMTP 和应用更新 |
| 设备适配 | 桌面与移动端布局，亮色与暗色模式 |

网站与插件提供 10 套中文主题：**经典、海洋、霓虹、纸笺、青柠、黑金、液态玻璃、网址簿、效率台、起始页**。既有色彩和光影风格，也有适合密集网址、工作分类和简洁主页的布局。

## 源码开发

仅源码开发需要安装 **.NET 10 SDK** 和 **Node.js 24**（Vite 最低支持 Node.js 22.12）。使用 SQLite 开发时无需 MySQL。

复制 `src/NestHub.Api/appsettings.json` 为 `src/NestHub.Api/appsettings.Development.json`，修改以下配置，保留模板中其他字段：

```json
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=AppData/nesthub.db",
    "AutoSyncStructure": true
  },
  "SuperAdmin": {
    "Email": "admin@example.com",
    "Password": "请替换为自己的强密码",
    "DisplayName": "超级管理员"
  },
  "Jwt": {
    "Issuer": "NestHub.Api",
    "Audience": "NestHub.Web",
    "SecretKey": "请替换为至少32字符的随机密钥",
    "ExpireHours": 720
  }
}
```

本地 SQLite 相对路径以 API 内容目录为准。开发配置不应提交到仓库。

在项目根目录启动后端（会构建前端）：

```bash
dotnet run --project src/NestHub.Api
```

访问 `http://localhost:5186`。需要前端热更新时，另开终端运行：

```bash
cd src/NestHub.Web
npm ci
npm run dev
```

前端开发地址为 `http://localhost:5173`，已配置后端代理。

技术栈：ASP.NET Core 10、FreeSql、SQLite / MySQL、Vue 3、TypeScript、Vite、Pinia、Element Plus，使用 JWT 认证。

## 构建与发布

推送 `main` 后，GitHub Actions 自动构建网站、后端与插件，验证 SQLite、MySQL、迁移和数据持久化，再发布 AMD64 / ARM64 镜像到 GHCR。主分支发布 `latest` 与提交版本标签；`v*` 标签用于版本发布。构建记录见 [Docker 工作流](https://github.com/sunnyfancore/NestHub/actions/workflows/docker.yml)。

| 目录 / 文件 | 内容 |
| --- | --- |
| `src/NestHub.Api` | 后端接口、数据库与业务服务 |
| `src/NestHub.Web` | 网站与管理后台 |
| `extensions/nesthub` | 浏览器插件 |
| `docker-compose.yml` | 默认 SQLite 部署 |
| `docker-compose.mysql.yml` | MySQL 部署 |
| `docker-compose.update.yml` | 旧版叠加配置，新部署无需使用 |
| `docs/docker-deployment.md` | 部署、挂载、迁移、备份与恢复 |

## License

MIT
