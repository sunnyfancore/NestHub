# NestHub

一个开源的多租户书签导航管理器，参考 [OneNav](https://github.com/helloxz/onenav) 设计思路，使用现代技术栈完全重写。

## 功能特性

- **多租户架构** — 数据隔离，每个租户独立空间
- **书签管理** — 目录分类、拖拽排序、批量导入（浏览器 HTML 书签）、点击计数
- **门户页面** — 5 套主题（默认 / 海洋 / 极光 / 玻璃 / 霓虹 / 纸质），亮色 / 暗色模式
- **搜索引擎** — 可切换搜索引擎，内置搜索建议
- **图标自动获取** — 基于 favicon.png.pub 服务自动获取网站图标
- **书签分享** — 生成分享链接，支持公开访问
- **超管后台** — 租户管理、链接管理、分类管理、主题配置、数据备份与恢复
- **安全特性** — JWT 认证、密码重置、邮箱通知（SMTP）
- **Docker 一键部署** — docker-compose 即可上线
- **响应式设计** — 适配桌面端与移动端

## 技术栈

| 层 | 技术 |
|---|------|
| 后端 | ASP.NET Core 10 + FreeSql + MySQL |
| 前端 | Vue 3 + TypeScript + Vite + Pinia + Element Plus |
| 认证 | JWT Bearer Token |
| 部署 | Docker + docker-compose |

## 项目结构

```
NestHub/
├── Dockerfile
├── docker-compose.yml
├── src/
│   ├── NestHub.Api/              # 后端 API
│   │   ├── Controllers/          # API 控制器
│   │   ├── Contracts/            # DTO 定义
│   │   ├── Domain/Entities/      # 实体模型
│   │   ├── Infrastructure/       # 中间件、JWT、数据种子
│   │   └── Services/             # 业务逻辑
│   └── NestHub.Web/              # 前端 SPA
│       └── src/
│           ├── api/              # API 调用
│           ├── components/       # 公共组件
│           ├── layouts/          # 布局
│           ├── router/           # 路由
│           ├── stores/           # Pinia 状态
│           └── views/            # 页面视图
```

## 快速开始

### 前置要求

- .NET 10 SDK
- Node.js 18+
- MySQL 5.7+

### 1. 克隆项目

```bash
git clone https://github.com/sunnyfancore/NestHub.git
cd NestHub
```

### 2. 配置数据库

复制配置模板并修改：

```bash
cp src/NestHub.Api/appsettings.json src/NestHub.Api/appsettings.Development.json
```

编辑 `appsettings.Development.json`，填入你的 MySQL 连接信息：

```json
{
  "Database": {
    "ConnectionString": "Server=localhost;Port=3306;Database=nesthub;User ID=root;Password=your_password;Charset=utf8mb4;SslMode=None;",
    "AutoSyncStructure": true
  }
}
```

> `AutoSyncStructure: true` 会自动创建表结构，无需手动执行 SQL。

### 3. 启动后端

```bash
dotnet run --project src/NestHub.Api
```

首次启动会自动完成：
- 数据库表结构同步
- 超级管理员账号创建

访问 `http://localhost:5186` 即可打开。

### 4. 前端开发（可选）

```bash
cd src/NestHub.Web
npm install
npm run dev
```

Vite 开发服务器地址 `http://localhost:5173`，已配置 API 代理。

### 5. Docker 部署

```bash
# 构建并发布
dotnet publish src/NestHub.Api/NestHub.Api.csproj -c Release -o publish "-p:SkipFrontendBuild=true"

# 启动
docker compose up -d
```

> 需先在 `appsettings.Production.json` 中配置生产环境数据库连接。

## 配置说明

| 配置项 | 说明 | 所在文件 |
|--------|------|----------|
| `Database:ConnectionString` | MySQL 连接字符串 | `appsettings.{env}.json` |
| `SuperAdmin:Email` | 超管初始邮箱 | `appsettings.{env}.json` |
| `SuperAdmin:Password` | 超管初始密码 | `appsettings.{env}.json` |
| `Jwt:SecretKey` | JWT 签名密钥（至少32字符） | `appsettings.{env}.json` |
| `Jwt:ExpireHours` | Token 过期时间（小时） | `appsettings.{env}.json` |
| `Cors:AllowedOrigins` | 允许的跨域来源 | `appsettings.{env}.json` |

## License

MIT
