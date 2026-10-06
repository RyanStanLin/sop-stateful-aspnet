# ASP.NET Core Personal PaaS template

从 **Use this template → Create a new repository** 创建独立仓库，owner 可选 `RyanStanLin` 或 `Deploy-ryanl-in`，公有和私有均支持。

```sh
git clone https://github.com/RyanStanLin/你的仓库.git
cd 你的仓库
dotnet restore --locked-mode
dotnet run --project src/App
```

需要 .NET SDK 10.0.401。本地访问 `http://localhost:8080`（先设置 `ASPNETCORE_URLS=http://localhost:8080`）；`/healthz`、`/api/hello`、WebSocket `/ws` 默认可用，无需应用密钥。WebSocket 原样回传 text/binary，支持分片，单消息上限 64 KiB。

```sh
dotnet format --verify-no-changes
dotnet test -c Release
git add .
git commit -m "Build my app"
git push origin main
```

首次本地 push 自动执行 CI、GHCR 构建、部署和 HTTPS，访问 `仓库名.ryanl.in`。以后 `git pull` 同步自己的仓库；模板更新不会改写已有项目。

## PostgreSQL、Redis 和 JSON 持久文件

数据访问实现已经包含 Npgsql、StackExchange.Redis。启用完整示例：

1. 把 `examples/postgres-redis.json` 复制为 `paas.json`。
2. 仓库 **Settings → Secrets and variables → Actions → New repository secret**，名称 `PAAS_SECRETS`，内容如下，各密码独立随机生成，不能使用示例占位值：

```json
{"POSTGRES_PASSWORD":"随机数据库密码","REDIS_PASSWORD":"随机Redis密码","API_TOKEN":"随机API测试令牌"}
```

3. commit/push；Web 服务通过 `postgres:5432` 和 `redis:6379` 连接本仓库隔离网络，数据库端口不公开。`DATABASE_PASSWORD` 引用同一个 `POSTGRES_PASSWORD` 值。
4. `/api/data/postgres/{key}` 与 `/api/data/redis/{key}` 支持 GET/PUT，PUT JSON 为 `{"value":"hello"}`。`/api/data/config` 支持 GET/PUT 任意不超过 64 KiB 的 JSON，保存到应用命名卷 `/data/config.json`。这些接口都必须发送 `X-Demo-Token: <API_TOKEN>`，没有令牌返回401。

`CONFIG_PATH` 只决定应用内文件路径；平台持久化由 `volumes` 声明决定。容器根文件系统只读，`/tmp` 在容器停止后会丢失，命名卷会在 stop、restart、delete 和后续 start 后保留。PostgreSQL、Redis 数据卷同样保留。普通 push 不会轮换现有数据库账号密码；详见 Wiki。

本地可以复制 `.env.example` 为 `.env.local`，填写自己的开发连接配置，然后 `set -a; source .env.local; set +a` 再运行应用。不要把 `.env.local`、`local-data`、真实密码或令牌提交。默认本地运行不连接外部数据库；启用依赖后健康检查同时验证数据库和Redis。

## 自定义域名、共享接口与运维

在 `paas.json` 把 `services.web.domain` 改为 `myproject.ryanl.in` 即可。push 成功后新域名生效，原域名路由被移除；不会新增逐项目 DNS。

多个服务共用域名时，每个参与服务明确配置同一个 `routing.sharedGroup`，可选 `routing.paths: ["/api/user", "/api/*"]`。路径是过滤器，不会删除前缀；未填路径会收到所有请求。各应用自己定义接口；404表示未处理，只有一个非404响应会返回客户端，多个服务响应返回409“多服务冲突”。Actions summary 列出同域名其他仓库/服务。WebSocket 按同样的握手冲突规则处理。

**Actions → PaaS operations → Run workflow** 提供 `start`、`stop`、`restart`、`delete`、`cleanup-images`、状态/日志/路由、重新部署、回滚和加密备份/恢复。操作作用于本仓库整个应用栈；delete 保留数据，start 可重建。cleanup-images 只删除 VPS 上本仓库未使用的应用镜像，运行和停止容器正在使用的镜像不会删除，GHCR 包保留。

完整 SOP、变量解释、数据库密码、持久化、共享路由及操作细节见 [平台 Wiki](https://github.com/Deploy-ryanl-in/personal-paas/wiki)。应用总预算768 MiB包含更新候选；示例192 MiB Web +256 MiB PostgreSQL +64 MiB Redis，Web更新时共704 MiB。
