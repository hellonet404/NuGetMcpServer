# NuGetMcpServer

一个基于 [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) 的 NuGet 包查询服务器，使用 .NET 10 与官方 [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) SDK 实现。

服务器既是 **ASP.NET Core WebAPI** 应用，也通过标准输入输出（stdio）与 MCP 客户端通信，向 LLM（如 Claude、Cursor 等）提供三类工具：**在线查询 nuget.org** 上的包版本、**查询需要认证的私有 NuGet 仓库**（如阿里云云效制品仓库）中的包版本，以及**扫描本地扁平化包目录**中的 `.nupkg` 文件。适用于需要让 AI 助手确认 NuGet 包是否存在、最新稳定版本号是多少、某版本是否已发布到本地或私有仓库等场景。

同时暴露**等价的 REST 接口**（`/api/*`，包装与 MCP 工具相同的服务），并内置 **Scalar**（.NET 10 最新的 OpenAPI/Swagger UI，地址 `/scalar/v1`），可在浏览器中直接调试查询接口。

## ✨ 功能特性

- 🌐 **在线版本查询**：直连 NuGet 官方 API（`https://api.nuget.org/v3-flatcontainer`），获取包的最新稳定版本与完整版本列表。
- 🔐 **私有仓库查询**：支持带用户名/密码认证的私有 NuGet v3 仓库（如阿里云云效制品仓库），自动解析 service index 并查询包版本，支持配置多个私有源。
- 📦 **本地包扫描**：扫描本地目录中的 `{包ID}.{版本}.nupkg` 文件，支持按包 ID 过滤（不区分大小写、子串匹配）。
- 📊 **版本智能排序**：所有版本均按 `NuGet.Versioning` 的语义版本规则从新到旧排序，预发布版本与稳定版本区分处理。
- ⚙️ **零 API Key**：使用公开的 NuGet v3 API，无需注册或付费。
- 🔌 **标准 MCP 协议**：基于官方 .NET SDK，通过 `WithStdioServerTransport()` 使用 stdio 传输，兼容所有主流 MCP 客户端。

## 🧰 提供的工具

| 工具名 | 类型 | 说明 |
| --- | --- | --- |
| `get_latest_package_version` | 在线 | 获取指定 NuGet 包的最新稳定版本号（若尚无稳定版，则提示最新预发布版本） |
| `list_package_versions` | 在线 | 列出包的最新 N 个版本（含预发布，默认 10 个，最多 50 个），按最新优先 |
| `list_local_packages` | 本地 | 列出本地目录中的所有包及其版本，可按包 ID 子串过滤 |
| `get_local_package_versions` | 本地 | 列出指定包在本地目录中的所有版本，用于确认某版本是否已下载到本地 |
| `list_private_sources` | 私有 | 列出所有已配置的私有 NuGet 仓库名称（如阿里云云效制品仓库） |
| `get_private_latest_package_version` | 私有 | 获取指定私有仓库中包的最新稳定版本号（若尚无稳定版，则提示最新预发布版本） |
| `list_private_package_versions` | 私有 | 列出指定私有仓库中包的最新 N 个版本（含预发布，默认 10 个，最多 50 个），按最新优先 |

## 🌐 WebAPI 与 REST 接口（Scalar 调试）

项目已从纯控制台改造为 **ASP.NET Core WebAPI**：MCP stdio 服务器保持原样运行，同时宿主进程额外提供一组与 MCP 工具**共享同一服务**的 REST 接口，并通过内置的 **Scalar**（.NET 10 最新的 OpenAPI/Swagger UI）在浏览器中直接调试。

### 启动

```bash
dotnet run
```

开发环境下默认地址（见 `Properties/launchSettings.json`）：

- Scalar UI（Swagger）：`http://localhost:60826/scalar/v1`（HTTPS 为 `https://localhost:60825`）
- OpenAPI 文档：`http://localhost:60826/openapi/v1.json`

> 说明：进程同时承担 MCP stdio（JSON-RPC 走 stdout）与 Kestrel HTTP。在普通终端里运行即可两者并存；当 MCP 客户端作为子进程拉起时，stdin/stdout 由客户端接管，HTTP 端口仍然可用。

### 可用 REST 接口

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| GET | `/api/health` | 健康检查 |
| GET | `/api/nuget/latest?packageId=` | 查询 nuget.org 最新稳定版本 |
| GET | `/api/nuget/versions?packageId=&count=` | 列出 nuget.org 版本（最新优先） |
| GET | `/api/nuget/local?search=` | 列出本地包目录（可按 ID 过滤） |
| GET | `/api/nuget/private/sources` | 列出私有源名称 |
| GET | `/api/nuget/private/versions?sourceName=&packageId=` | 列出私有源中包版本 |

所有接口均可在 Scalar 页面里点击测试；返回 JSON 为 camelCase 命名。

## 📋 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download) 或更高版本
- Node.js 22.19.0+（仅本地测试 / 客户端集成时需要，用于运行 MCP Inspector）
- Docker Desktop（仅容器部署时需要，见 [Docker Desktop 部署](#-docker-desktop-部署)）

## ⚙️ 配置

服务器配置读取 `appsettings.json` 中的 `Mcp` 与 `NuGet` 两个配置节：

```json
{
  "Mcp": {
    "Transport": "stdio",
    "HttpPath": "/mcp"
  },
  "NuGet": {
    "LocalPackagesPath": "D:\\Code\\NugetPackages",
    "PrivateSources": [
      {
        "Name": "aliyun-oucp",
        "ServiceIndexUrl": "https://packages.aliyun.com/66c40265f168c2ed4c91bd48/nuget/repo-oucp/index.json",
        "Username": "你的阿里云仓库用户名",
        "Password": "你的阿里云仓库密码"
      }
    ]
  }
}
```

| 配置项 | 说明 |
| --- | --- |
| `Mcp:Transport` | MCP 传输方式：`stdio`（默认，客户端以子进程拉起）或 `http`（Streamable HTTP，容器部署使用） |
| `Mcp:HttpPath` | MCP HTTP 端点路径，仅在 `Transport` 为 `http` 时生效，默认 `/mcp` |
| `NuGet:LocalPackagesPath` | 本地扁平化 NuGet 包目录路径，目录中直接存放 `{包ID}.{版本}.nupkg` 文件（必填，缺失时服务器启动会报错） |
| `NuGet:PrivateSources` | 私有 NuGet v3 仓库数组（可选）。每个仓库含 `Name`（唯一名称，工具用 `sourceName` 参数选择）、`ServiceIndexUrl`（v3 service index 地址，即仓库指南中给出的 `index.json` 完整 URL）、`Username` / `Password`（Basic Auth 凭证） |

> **私有仓库工作原理**：服务器先带 Basic Auth 请求 `ServiceIndexUrl`（service index），解析出 `PackageBaseAddress/3.0.0`（flat container）资源地址，再带认证请求 `{flatContainer}/{包ID}/index.json` 获取版本列表。认证方式与 `dotnet nuget add source -u 用户名 -p 密码` 一致。

### 私有仓库凭证的配置与覆盖

- **直接配置**：把 `Username` / `Password` 写入 `appsettings.json`（最简单，但凭证明文落盘）。
- **环境变量覆盖**：凭据可通过环境变量覆盖，避免明文落盘。ASP.NET Core 配置系统按层级用双下划线 `__` 分隔键名，例如：

  ```powershell
  # 覆盖第 1 个私有源（下标从 0 开始）的密码
  $env:NuGet__PrivateSources__0__Password = "你的真实密码"
  ```

- **`${VAR}` 引用**：配置值也支持 `${环境变量名}` 语法，把真实凭据放到环境变量、配置里只写占位引用，例如 `"Password": "${ALIYUN_NUGET_PASSWORD}"`。

> **认证头与重定向**：`HttpClient` 默认在跨主机重定向时丢弃 `Authorization` 头（安全默认，防止凭据泄露给第三方主机），同主机重定向则保留。阿里云仓库走标准 v3 流程通常无碍；若配置正确却全部返回 401，请检查仓库是否把请求重定向到了其他主机。

## 🚀 本地运行与测试

### 1. 构建项目

在仓库根目录执行：

```bash
dotnet build
```

### 2. 使用 MCP Inspector 进行本地测试（推荐）

[MCP Inspector](https://modelcontextprotocol.io/docs/2026-07-28/tools/inspector) 是 MCP 官方的开发者调试工具，以 Web 图形界面形式测试和调试 MCP 服务器。在仓库根目录运行：

```bash
npx @modelcontextprotocol/inspector dotnet run
```

> 首次运行 npx 会提示是否下载 `@modelcontextprotocol/inspector` 包，输入 `y` 确认即可（也可用 `npx -y` 自动确认）。Inspector 要求 Node.js 22.19.0 及以上版本。

运行后终端会输出一个带一次性会话令牌（one-time session token）的本地 URL，在浏览器中打开即可进入调试界面：

- 左侧查看服务器暴露的所有工具（`tools/list`）；
- 选择工具、填写参数后点击调用（`tools/call`），实时查看返回结果；
- 底部/侧边栏可监控完整的 JSON-RPC 请求/响应流量，便于排查协议问题。

### 本地测试的原理

MCP 采用 **JSON-RPC over stdio** 的通信方式：服务器通过标准输入（stdin）接收请求、通过标准输出（stdout）返回响应，客户端与服务器是两个独立的进程。

`npx @modelcontextprotocol/inspector dotnet run` 这条命令的本质是：

1. **Inspector 作为 MCP 客户端**：Inspector 会把你传入的 `dotnet run` 作为子进程启动，并接管它的 stdin/stdout，建立起一条 stdio 管道。
2. **服务器以子进程方式运行**：`dotnet run` 会先编译并启动本项目，服务器启动后通过 `WithStdioServerTransport()` 绑定在 stdio 传输上，等待来自 stdin 的 JSON-RPC 请求。
3. **Inspector 的 Web 界面作为桥梁**：Inspector 同时在本机起了一个本地 Web 服务，终端打印的 URL（带一次性令牌）就是它的入口。浏览器与 Inspector 通信，Inspector 再把请求转发到子进程服务器——于是你可以直接在浏览器里操作真实的服务器进程。
4. **协议初始化与调试**：Inspector 自动完成 MCP 握手（`initialize`），随后调用 `tools/list` 枚举工具、`tools/call` 调用工具，并把每一帧 JSON-RPC 消息展示在界面上，方便你观察请求/响应是否符合协议规范。

一个值得注意的实现细节：本项目在 `Program.cs` 中把**所有日志都重定向到标准错误流（stderr）**。这是因为 stdout 承担着 JSON-RPC 协议数据，任何多余输出都会破坏协议——Inspector 的调试界面也会把 stderr 输出显示出来，便于你查看服务器日志。

## 📦 AOT 原生发布

本项目已配置为支持 .NET AOT（Ahead-of-Time）原生发布：把程序连同依赖的 .NET 运行时一起编译为单一原生可执行文件。目标机器无需安装 .NET 运行时，启动更快、内存占用更低、反编译难度更高。

> **AOT 前提**：发布机需安装 .NET 10 SDK，并具备对应平台的 C++ 原生编译工具链（Windows 上为 Visual Studio 的「使用 C++ 的桌面开发」工作负载）。

### 项目中的 AOT 相关配置

`NuGetMcpServer.csproj` 中已包含：

- `<PublishAot>true</PublishAot>`：启用 AOT 发布。
- `<DebugType>none</DebugType>` 与 `<DebugSymbols>false</DebugSymbols>`：发布时不生成 .pdb 调试符号，减小分发体积。
- JSON 序列化使用 `JsonSerializerContext` source generator（`Services/NuGetCatalogClient.cs` 中的 `VersionIndexJsonContext` 与 `Services/PrivateNuGetCatalogClient.cs` 中的 `PrivateServiceIndexJsonContext`），避免反射式 System.Text.Json 在 AOT 下抛出 `NotSupportedException`。

### 发布命令

以 Windows x64 为例：

```bash
dotnet publish -c Release -r win-x64
```

产物位于 `bin/Release/net10.0/win-x64/publish/`，其中 `NuGetMcpServer.exe` 即最终原生可执行文件。

> 其他平台将 `win-x64` 替换为 `linux-x64`、`osx-arm64` 等即可，目标平台需与运行环境一致。

> 容器内同样可以 AOT 发布，需改用 `sdk:10.0-aot` 镜像，仓库中的 `Dockerfile.aot` 为现成方案（默认不启用）。差异对比见 [DOCKER.md 第 11 节](DOCKER.md#11-可选native-aot-构建)。

### 在 MCP 客户端中使用发布后的 exe

发布完成后，可直接以原生 exe 作为 MCP 服务器启动命令：

```json
{
  "mcpServers": {
    "nuget": {
      "command": "C:/path/to/publish/NuGetMcpServer.exe"
    }
  }
}
```

### 常见问题

- **发布报 MSB4216（任务宿主创建失败）**：这是 .NET SDK 10.0.302 在 Windows 上的已知问题，仅影响 Windows 且与代码无关。可升级 SDK 至 10.0.100+ 或降级至 9.0.306 解决（参见 [dotnet/msbuild#12751](https://github.com/dotnet/msbuild/issues/12751)）。
- **AOT 下 JSON 序列化异常**：本项目已通过 `JsonSerializerContext` 预生成序列化代码，不会出现反射式序列化导致的 `NotSupportedException`。若后续新增需要 JSON 序列化的类型，请记得在 `VersionIndexJsonContext` 中通过 `[JsonSerializable]` 注册。
- **发布后修改配置**：AOT 发布产物仍读取同目录下的 `appsettings.json`，请将配置文件一并复制到部署目录。
- **私有仓库查询返回 401**：优先检查 `NuGet:PrivateSources` 中该源的 `Username` / `Password` 是否正确、是否被环境变量覆盖为错误值；再确认仓库 URL 是否发生了跨主机重定向（见上方「认证头与重定向」说明）。

## 🐳 Docker Desktop 部署

除本地 stdio 方式外，本项目可打包为容器常驻运行，此时 MCP 改用 **Streamable HTTP** 传输，客户端通过 URL 连接而不再拉起子进程。

```bash
docker compose up -d
```

启动后的地址（容器内监听 18081，宿主机映射 18080，仅绑定 `127.0.0.1`）：

| 用途 | 地址 |
| --- | --- |
| MCP 服务端点 | `http://localhost:18080/mcp` |
| 健康检查 | `http://localhost:18080/api/health` |
| REST 接口 | `http://localhost:18080/api/nuget/...` |

需要查询私有仓库时，复制 `.env.example` 为 `.env` 填写凭证后叠加启动：

```bash
docker compose --env-file .env -f docker-compose.yml -f docker-compose.private.yml up -d
```

> 完整流程与每个配置项的逐项说明见 **[DOCKER.md](DOCKER.md)**，包含构建参数、目录挂载、健康检查、运维命令与常见问题。

## 🔌 与 MCP 客户端集成

本服务器支持两种传输方式，由配置项 `Mcp:Transport` 决定：stdio（默认，本地开发）与 HTTP（容器部署）。

**stdio 方式**：启动命令为 `dotnet run`（可在 `NuGetMcpServer.csproj` 所在目录执行）。在任意支持 MCP 的客户端中，添加一个 `command` 类型的 MCP 服务器即可，例如：

```json
{
  "mcpServers": {
    "nuget": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/NuGetMcpServer"]
    }
  }
}
```

> 客户端配置中的路径请替换为实际的项目绝对路径。部分客户端（如 Cursor）会从 MCP 配置目录读取类似格式的 JSON 文件。

**HTTP 方式**（容器部署后）：直接填写服务地址，无需本机安装 .NET。

```json
{
  "mcpServers": {
    "nuget": {
      "url": "http://localhost:18080/mcp"
    }
  }
}
```

## 📁 项目结构

```
NuGetMcpServer/
├── Program.cs                          # 入口：WebApplication 宿主、MCP HTTP 端点、OpenAPI/Scalar、REST 端点
├── appsettings.json                    # 本机配置：Mcp:Transport + NuGet:LocalPackagesPath + NuGet:PrivateSources
├── NuGetMcpServer.csproj               # 项目文件（.NET 10 Web SDK + ModelContextProtocol 2.2.0）
├── Properties/launchSettings.json      # 开发环境端口（https:60825 / http:60826）
├── DOCKER.md                           # Docker Desktop 部署手册（配置项逐项说明）
├── Dockerfile                          # 多阶段构建：SDK 编译 + ASP.NET 运行时，非 root 运行
├── docker-compose.yml                  # 容器编排：端口 18080→18081、包目录挂载、健康检查
├── docker-compose.private.yml          # 可选叠加：注入私有源认证凭证
├── appsettings.Docker.json             # 镜像内配置基线（HTTP 传输，不含任何凭据）
├── .env.example                        # 环境变量模板，复制为 .env 后填写真实值
├── Api/
│   ├── NuGetApiEndpoints.cs            # REST 端点：/api/*，包装与 MCP 工具相同的服务
│   ├── Dtos.cs                         # REST 响应 DTO（record 类型）
│   └── AppJsonSerializerContext.cs     # AOT 安全 JSON 序列化上下文（source generator）
├── Settings/
│   └── LocalSettingOptions.cs          # 配置模型：本地包目录 + 私有源列表（含校验）
├── Services/
│   ├── ServiceExtension.cs             # DI 注册：HttpClient、配置绑定、MCP 服务器
│   ├── NuGetCatalogClient.cs           # 在线查询 nuget.org v3-flatcontainer 目录 API
│   ├── PrivateNuGetCatalogClient.cs    # 私有仓库（Basic Auth）查询：解析 service index + flat container
│   └── LocalNuGetPackageService.cs     # 扫描本地 .nupkg 目录，解析 包ID.版本 文件名
└── Tools/
    ├── NuGetTools.cs                   # 在线工具：get_latest_package_version / list_package_versions
    ├── PrivateNuGetTools.cs            # 私有仓库工具：list_private_sources / get_private_latest_package_version / list_private_package_versions
    └── LocalNuGetTools.cs              # 本地工具：list_local_packages / get_local_package_versions
```

## 🧱 技术栈

- **.NET 10**（`net10.0`，`Microsoft.NET.Sdk.Web`）
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) 2.2.0（官方 C# MCP SDK）
- [NuGet.Versioning](https://www.nuget.org/packages/NuGet.Versioning) 7.9.0（语义版本解析与排序）
- [Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) 10.0.11（OpenAPI 3.1 文档生成，兼容 Native AOT）
- [Scalar.AspNetCore](https://www.nuget.org/packages/Scalar.AspNetCore) 2.17.2（.NET 10 最新的 OpenAPI/Swagger UI）

## 📄 License

[MIT](LICENSE.txt)
