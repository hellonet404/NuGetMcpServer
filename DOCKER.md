# NuGetMcpServer Docker Desktop 部署手册

本文档说明如何把本项目打包为镜像并部署到本机 Docker Desktop，覆盖从构建、启动、验证到 MCP 客户端接入的完整流程，并逐项解释每个配置文件中每一项的含义。

---

## 1. 部署形态说明

本项目同一份代码支持两种 MCP 传输方式，由配置项 `Mcp:Transport` 决定：

| 运行方式 | 传输 | 适用场景 |
| --- | --- | --- |
| 本机 `dotnet run` | stdio | 本地开发调试，MCP 客户端以子进程方式拉起服务器 |
| Docker 容器 | Streamable HTTP | 服务常驻后台，MCP 客户端通过 URL 连接 |

容器内不能使用 stdio：容器以后台进程运行，没有 MCP 客户端接管它的标准输入输出。因此镜像内固定使用 HTTP 传输。

部署后的访问地址：

| 用途 | 地址 |
| --- | --- |
| MCP 服务端点 | `http://localhost:18080/mcp` |
| 健康检查 | `http://localhost:18080/api/health` |
| REST 查询接口 | `http://localhost:18080/api/nuget/...` |

端口设计：容器内部监听 `18081`，宿主机映射为 `18080`，并且只绑定 `127.0.0.1`，不对局域网暴露。

---

## 2. 前置条件

- 已安装并启动 Docker Desktop（需切换到 Linux 容器模式，这是默认模式）
- 宿主机可访问 `mcr.microsoft.com`（拉取 .NET 10 基础镜像）与 `nuget.org`（还原依赖）
- 若需要扫描本地 `.nupkg`，宿主机上需存在对应目录，并已在 Docker Desktop 的 `Settings → Resources → File sharing` 中允许该磁盘共享

验证 Docker 可用：

```bash
docker version
```

---

## 3. 相关文件清单

| 文件 | 作用 |
| --- | --- |
| `Dockerfile` | 定义镜像如何构建：编译、裁剪、运行用户、端口、健康检查、启动命令 |
| `docker-compose.yml` | 定义容器如何运行：端口映射、环境变量、目录挂载、重启策略、健康检查 |
| `docker-compose.private.yml` | 可选叠加文件，仅在需要私有 NuGet 源时使用，注入认证凭据 |
| `appsettings.Docker.json` | 镜像内使用的配置基线，不含任何凭据 |
| `.dockerignore` | 指定哪些文件不进入构建上下文，防止本地凭据与编译产物混入镜像 |
| `.env.example` | 环境变量模板，复制为 `.env` 后填写真实值 |
| `appsettings.json` | 本机开发用配置，不会进入镜像 |

---

## 4. 配置文件逐项说明

### 4.1 `Dockerfile`

采用多阶段构建：第一阶段用 SDK 镜像编译，第二阶段只保留运行时和发布产物，最终镜像不含源码和 SDK。

**构建阶段**

| 指令 | 含义 |
| --- | --- |
| `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build` | 使用 .NET 10 SDK 镜像作为编译环境，命名为 `build` 阶段 |
| `WORKDIR /src` | 后续命令的工作目录设为 `/src` |
| `COPY ["NuGetMcpServer.csproj", "./"]` | 先只复制项目文件。这样只要依赖没变，下面的还原层就能命中缓存，不必每次重新下载包 |
| `RUN dotnet restore "NuGetMcpServer.csproj"` | 还原 NuGet 依赖 |
| `COPY . .` | 复制其余源码（受 `.dockerignore` 过滤） |
| `RUN dotnet publish ...` | 执行发布，产物输出到 `/app/publish` |

发布命令的各个参数：

| 参数 | 含义 |
| --- | --- |
| `--configuration Release` | 使用 Release 配置编译 |
| `--output /app/publish` | 指定发布产物目录 |
| `--no-restore` | 跳过还原，复用上一层已还原的结果，避免重复耗时 |
| `--self-contained false` | 不打包运行时，依赖运行时基础镜像中已有的 .NET。镜像更小 |
| `-p:PublishAot=false` | 覆盖 `.csproj` 中的 `PublishAot=true`。AOT 需要原生编译工具链，且产物与平台绑定；容器里用常规发布更快更稳。此参数只在构建镜像时生效，不影响你在 Windows 上的 AOT 发布 |

**运行阶段**

| 指令 | 含义 |
| --- | --- |
| `FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final` | 运行时镜像，只含 ASP.NET Core 运行时，不含 SDK |
| `WORKDIR /app` | 应用工作目录 |
| `RUN apt-get update && apt-get install ... curl ...` | 安装 `curl`，供容器内健康检查使用。`--no-install-recommends` 不装推荐包，`rm -rf /var/lib/apt/lists/*` 清理索引缓存，两者都是为了减小镜像体积 |
| `RUN if ! getent group app ...` | 创建非 root 运行账号 `app`。判断是否已存在，是因为 .NET 10 的 Ubuntu 基础镜像已预置同名用户组，直接 `groupadd` 会报错导致构建失败 |
| `COPY --from=build /app/publish .` | 从构建阶段取出发布产物，源码和 SDK 不进入最终镜像 |
| `COPY appsettings.Docker.json ./appsettings.json` | 用不含凭据的 Docker 配置覆盖为应用读取的 `appsettings.json` |
| `ENV ASPNETCORE_URLS=http://+:18081` | 应用监听容器内所有网卡的 `18081` 端口。`+` 表示所有地址，容器内必须监听 `0.0.0.0` 而不是 `localhost`，否则宿主机无法访问 |
| `EXPOSE 18081` | 声明容器对外提供的端口，属于元数据说明，实际开放由端口映射决定 |
| `HEALTHCHECK ...` | 容器健康检查，详见下表 |
| `USER app` | 切换到非 root 用户运行，降低容器逃逸风险。放在所有 `COPY` 之后，避免权限问题 |
| `ENTRYPOINT ["dotnet", "NuGetMcpServer.dll"]` | 容器启动命令 |

健康检查参数：

| 参数 | 含义 |
| --- | --- |
| `--interval=30s` | 每 30 秒检查一次 |
| `--timeout=5s` | 单次检查超过 5 秒判定为失败 |
| `--start-period=15s` | 启动后的 15 秒宽限期内失败不计入重试次数，避免应用尚未就绪就被判定不健康 |
| `--retries=3` | 连续失败 3 次才标记为 `unhealthy` |
| `CMD curl --fail --silent http://localhost:18081/api/health \|\| exit 1` | 检查命令。`--fail` 让 HTTP 错误码返回非零退出码，`--silent` 不输出进度 |

### 4.2 `docker-compose.yml`

| 配置项 | 含义 |
| --- | --- |
| `services.nuget-mcp` | 服务名。后续所有 `docker compose` 子命令用这个名字引用容器 |
| `build.context: .` | 构建上下文为当前目录，即发送给 Docker 引擎的文件范围 |
| `build.dockerfile: Dockerfile` | 使用的 Dockerfile 路径 |
| `image: nuget-mcp-server:local` | 构建产出的镜像名与标签 |
| `container_name: nuget-mcp-server` | 固定容器名，便于在 Docker Desktop 界面和命令中识别 |
| `restart: unless-stopped` | 容器异常退出或 Docker 重启后自动拉起，但手动 `stop` 后不会自动启动 |
| `ports: "127.0.0.1:18080:18081"` | 端口映射，格式为 `宿主机IP:宿主机端口:容器端口`。绑定 `127.0.0.1` 意味着只有本机能访问，局域网内其他机器连不上 |

环境变量：

| 变量 | 含义 |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT: Production` | 运行环境。设为 `Production` 时不启用 Scalar UI 与 OpenAPI 文档 |
| `ASPNETCORE_URLS: "http://+:18081"` | 应用监听地址，与 Dockerfile 中保持一致 |
| `Mcp__Transport: http` | 启用 Streamable HTTP 传输。双下划线 `__` 是 ASP.NET Core 配置的层级分隔符，等价于 JSON 中的 `Mcp:Transport` |
| `Mcp__HttpPath: /mcp` | MCP 端点路径 |
| `NuGet__LocalPackagesPath: /packages` | 本地包扫描目录，指向容器内路径，而非宿主机路径 |

目录挂载：

| 配置项 | 含义 |
| --- | --- |
| `type: bind` | 绑定挂载，把宿主机真实目录映射进容器（区别于 Docker 托管的 volume） |
| `source: "${NUGET_PACKAGES_PATH:-D:/Code/NugetPackages}"` | 宿主机目录。优先读环境变量 `NUGET_PACKAGES_PATH`，未设置时回退到 `:-` 后面的默认值 |
| `target: /packages` | 容器内挂载点，与 `NuGet__LocalPackagesPath` 对应 |
| `read_only: true` | 只读挂载。本项目只需扫描 `.nupkg`，只读可防止容器误写宿主机目录 |

> 注意：使用 `http_download` 工具下载包到宿主机目录时，该只读设置会阻止写入。若确有此需求，把 `read_only` 改为 `false`。

健康检查部分与 Dockerfile 中含义相同，Compose 中的定义会覆盖镜像内的定义，便于不重建镜像就调整检查策略。

### 4.3 `docker-compose.private.yml`

只有需要查询私有 NuGet 仓库时才叠加使用，作用是把凭据以环境变量形式注入容器，避免写进镜像或提交到仓库。

| 配置项 | 含义 |
| --- | --- |
| `NuGet__PrivateSources__0__Name` | 第 1 个私有源的名称，下标从 `0` 开始。MCP 工具通过 `sourceName` 参数引用它 |
| `NuGet__PrivateSources__0__ServiceIndexUrl` | 私有源的 NuGet v3 service index 地址 |
| `NuGet__PrivateSources__0__Username` | Basic Auth 用户名 |
| `NuGet__PrivateSources__0__Password` | Basic Auth 密码 |

语法 `${VAR:?提示信息}` 表示该变量必须提供，缺失时 Compose 直接报错并终止，避免容器带着空凭据启动后才在调用时失败。

配置多个私有源时，把下标递增即可，例如 `NuGet__PrivateSources__1__Name`。

### 4.4 `appsettings.Docker.json`

镜像内的配置基线，构建时被复制为容器中的 `appsettings.json`。

| 配置项 | 含义 |
| --- | --- |
| `Mcp.Transport: "http"` | 镜像默认使用 HTTP 传输 |
| `Mcp.HttpPath: "/mcp"` | MCP 端点默认路径 |
| `NuGet.LocalPackagesPath: "/packages"` | 容器内的包目录 |
| `NuGet.PrivateSources: []` | 空数组。镜像本身不含任何凭据，需要时由环境变量注入 |

这份文件与 Compose 中的环境变量存在重复，是有意为之：环境变量优先级更高，而这份基线保证即使直接 `docker run` 不带任何环境变量，容器也能以正确的 HTTP 模式启动。

### 4.5 `.dockerignore`

排除的内容不会进入构建上下文，既加快构建，也防止敏感文件进镜像。

| 条目 | 排除原因 |
| --- | --- |
| `.git/`、`.vs/` | 版本库与 IDE 缓存，与构建无关 |
| `bin/`、`obj/`、`artifacts/`、`publish/` | 宿主机（Windows）的编译产物，与容器（Linux）平台不符，混入会导致构建异常 |
| `*.user`、`*.suo` | IDE 个人配置 |
| `*.nupkg`、`*.snupkg` | 包文件，体积大且无需进镜像 |
| `*.log` | 日志文件 |
| `.env` | 含真实凭据，绝不能进镜像 |
| `appsettings.json`、`appsettings.*.json` | 排除全部本地配置，其中 `appsettings.json` 含明文凭据 |
| `!appsettings.Docker.json` | `!` 表示取消排除。上一行排除了所有配置文件，这里单独放行不含凭据的 Docker 基线配置 |

最后两行的顺序不能调换：`.dockerignore` 按自上而下的顺序匹配，后面的规则覆盖前面的。

### 4.6 `.env.example`

环境变量模板，本身不含真实凭据，可安全提交。使用时复制为 `.env`，`.env` 已被 `.dockerignore` 排除。

| 变量 | 含义 |
| --- | --- |
| `NUGET_PACKAGES_PATH` | 宿主机本地包目录。路径用正斜杠，例如 `D:/Code/NugetPackages` |
| `PRIVATE_NUGET_SOURCE_NAME` | 私有源名称，仅叠加 `docker-compose.private.yml` 时需要 |
| `PRIVATE_NUGET_SERVICE_INDEX_URL` | 私有源 service index 地址 |
| `PRIVATE_NUGET_USERNAME` | 私有源用户名 |
| `PRIVATE_NUGET_PASSWORD` | 私有源密码 |

### 4.7 `appsettings.json` 中的 `Mcp` 节

这份文件只用于本机开发，不进入镜像。

| 配置项 | 含义 |
| --- | --- |
| `Mcp.Transport: "stdio"` | 本地保持 stdio，MCP 客户端可继续用 `dotnet run` 拉起 |
| `Mcp.HttpPath: "/mcp"` | 仅在把 `Transport` 改为 `http` 时生效 |

本机临时体验 HTTP 模式，无需改文件，设置环境变量即可：

```bash
cmd /c "set Mcp__Transport=http&&dotnet run"
```

---

## 5. 构建与部署步骤

### 步骤一：准备环境变量（可选）

只在需要自定义本地包目录或启用私有源时执行：

```bash
copy .env.example .env
```

然后编辑 `.env` 填写真实值。若使用默认包目录且不需要私有源，可跳过本步骤。

### 步骤二：构建镜像

```bash
docker compose build
```

首次构建需要拉取 .NET 10 基础镜像，耗时较长；后续构建会命中缓存。

强制完整重建（排查缓存问题时使用）：

```bash
docker compose build --no-cache
```

### 步骤三：启动容器

不使用私有源：

```bash
docker compose up -d
```

使用私有源：

```bash
docker compose --env-file .env -f docker-compose.yml -f docker-compose.private.yml up -d
```

`-d` 表示后台运行。多个 `-f` 按顺序叠加，后面的文件覆盖前面的同名配置。

### 步骤四：确认状态

```bash
docker compose ps
```

`STATUS` 列出现 `Up ... (healthy)` 即为部署成功。首次启动时会先显示 `health: starting`，属于健康检查的宽限期，等待约 15 秒即可。

---

## 6. 部署验证

### 健康检查

```bash
curl.exe http://127.0.0.1:18080/api/health
```

预期返回：

```json
{"status":"ok"}
```

### REST 接口

```bash
curl.exe "http://127.0.0.1:18080/api/nuget/latest?packageId=Newtonsoft.Json"
```

### MCP 握手

```bash
curl.exe -X POST http://127.0.0.1:18080/mcp -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -H "MCP-Protocol-Version: 2025-11-25" --data-raw '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
```

预期返回 `serverInfo.name` 为 `NuGetMcpServer` 的 JSON-RPC 响应。

### 工具列表

```bash
curl.exe -X POST http://127.0.0.1:18080/mcp -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -H "MCP-Protocol-Version: 2025-11-25" --data-raw '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
```

应返回全部 9 个工具。

### 本地包目录挂载

```bash
docker compose exec nuget-mcp ls /packages
```

若输出宿主机目录中的 `.nupkg` 文件，说明挂载成功。

---

## 7. MCP 客户端接入

客户端运行在宿主机上：

```json
{
  "mcpServers": {
    "nuget": {
      "url": "http://localhost:18080/mcp"
    }
  }
}
```

客户端本身也运行在容器中时，改用 Docker Desktop 提供的宿主机别名：

```json
{
  "mcpServers": {
    "nuget": {
      "url": "http://host.docker.internal:18080/mcp"
    }
  }
}
```

部分客户端要求显式声明传输类型，此时补充 `"type": "http"`。

---

## 8. 日常运维命令

| 操作 | 命令 |
| --- | --- |
| 查看状态 | `docker compose ps` |
| 查看日志 | `docker compose logs -f nuget-mcp` |
| 重启 | `docker compose restart` |
| 停止并删除容器 | `docker compose down` |
| 改代码后重新部署 | `docker compose up -d --build` |
| 进入容器排查 | `docker compose exec nuget-mcp bash` |

注意应用日志全部写入 stderr（为兼容 stdio 模式下的 JSON-RPC 协议），这不影响 `docker compose logs` 查看。

---

## 9. 常见问题

**构建报 `groupadd: group 'app' already exists`**

.NET 10 基础镜像已预置 `app` 用户组。当前 Dockerfile 已加入存在性判断，若你自行修改过该段，请保留判断逻辑。

**宿主机 18080 端口被占用**

修改 `docker-compose.yml` 的端口映射，只改冒号左侧：

```yaml
ports:
  - "127.0.0.1:18090:18081"
```

容器内部端口 `18081` 不要改，否则需同步修改 `ASPNETCORE_URLS` 和两处健康检查。

**容器状态是 `unhealthy`**

先看日志 `docker compose logs nuget-mcp`。最常见原因是配置校验失败：`NuGet:LocalPackagesPath` 为空会导致启动即报错，该项是必填的。

**`/packages` 为空**

依次检查：宿主机目录是否真实存在；`.env` 中路径是否用了正斜杠；Docker Desktop 的 File sharing 是否允许该磁盘。注意目录不存在时应用不会报错，只会返回空列表。

**访问 `/scalar/v1` 返回 404**

Scalar UI 只在开发环境注册。临时启用可把 Compose 中的 `ASPNETCORE_ENVIRONMENT` 改为 `Development`，但该环境同时会输出更详细的错误信息，不建议长期保留。

**私有源查询返回 401**

确认启动时叠加了 `docker-compose.private.yml`，且 `.env` 中凭据正确。可执行下面的命令确认变量是否被注入（会显示密码，请在安全环境下操作）：

```bash
docker compose exec nuget-mcp printenv | findstr PrivateSources
```

---

## 10. 安全注意事项

- 当前 MCP 端点没有认证机制，安全性依赖端口只绑定 `127.0.0.1`。若要改为对外提供服务，必须先加上认证与 HTTPS。
- 仓库中的 `appsettings.json` 目前仍含明文凭据。它不会进入镜像，但建议改为占位值，真实凭据统一走环境变量。
- `.env` 不要提交到版本库，仅提交 `.env.example`。
- `http_download` 工具允许写入容器文件系统，当前包目录为只读挂载，可限制其影响范围。

---

## 11. 可选：Native AOT 构建

默认的 `Dockerfile` 关闭了 AOT，原因是标准的 `sdk:10.0` 镜像不含原生编译工具链。若要在容器内做 AOT 发布，需改用官方的 AOT 变体镜像，仓库中的 `Dockerfile.aot` 即为此方案，它不被 `docker-compose.yml` 引用，属于独立备选。

```bash
docker build -f Dockerfile.aot -t nuget-mcp-server:aot .
docker run -d --name nuget-mcp-aot -p 127.0.0.1:18080:18081 nuget-mcp-server:aot
```

两种方案的差异：

| 对比项 | 默认方案（`Dockerfile`） | AOT 方案（`Dockerfile.aot`） |
| --- | --- | --- |
| 构建镜像 | `sdk:10.0` | `sdk:10.0-aot`（含 clang、llvm、zlib1g-dev） |
| 运行镜像 | `aspnet:10.0` | `runtime-deps:10.0`（不含 .NET 运行时） |
| 产物形态 | `.dll` + 运行时 | 单个原生可执行文件，约 27 MB |
| 镜像体积 | 约 241 MB | 约 155 MB |
| 发布耗时 | 约 4 秒 | 约 48 秒 |

AOT 方案的实测结论：构建无任何 `IL2xxx` / `IL3xxx` 裁剪告警，容器启动后健康检查、MCP 握手、`tools/list`（9 个工具齐全）与工具实际调用均正常，即 `ModelContextProtocol.AspNetCore` 在本项目的用法下是 AOT 兼容的。

选择默认方案的原因：

- 构建耗时相差约 10 倍，频繁改代码重新部署时体感明显；AOT SDK 镜像首次拉取约 1.5 GB。
- AOT 把问题从编译期推迟到运行期。后续新增依赖反射的代码（例如新 DTO 漏了 `[JsonSerializable]` 注册），常规发布仍能运行，AOT 下会在调用时抛异常。

若以体积为首要目标，可把运行镜像换成 `runtime-deps:10.0-noble-chiseled-aot` 并改用不依赖 `curl` 的健康检查方式，整体可压缩到 40 MB 量级。
