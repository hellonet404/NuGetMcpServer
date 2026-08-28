# NuGetMcpServer

一个基于 [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) 的 NuGet 包查询服务器，使用 .NET 10 与官方 [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) SDK 实现。

服务器通过标准输入输出（stdio）与 MCP 客户端通信，向 LLM（如 Claude、Cursor 等）提供两类工具：**在线查询 nuget.org** 上的包版本，以及**扫描本地扁平化包目录**中的 `.nupkg` 文件。适用于需要让 AI 助手确认 NuGet 包是否存在、最新稳定版本号是多少、某版本是否已发布到本地等场景。

## ✨ 功能特性

- 🌐 **在线版本查询**：直连 NuGet 官方 API（`https://api.nuget.org/v3-flatcontainer`），获取包的最新稳定版本与完整版本列表。
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

## 📋 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download) 或更高版本
- Node.js 22.19.0+（仅本地测试 / 客户端集成时需要，用于运行 MCP Inspector）

## ⚙️ 配置

服务器配置读取 `appsettings.json` 中的 `NuGet` 配置节：

```json
{
  "NuGet": {
    "LocalPackagesPath": "D:\\Code\\NugetPackages"
  }
}
```

| 配置项 | 说明 |
| --- | --- |
| `NuGet:LocalPackagesPath` | 本地扁平化 NuGet 包目录路径，目录中直接存放 `{包ID}.{版本}.nupkg` 文件（必填，缺失时服务器启动会报错） |

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

## 🔌 与 MCP 客户端集成

本服务器采用 stdio 传输，启动命令为 `dotnet run`（可在 `NuGetMcpServer.csproj` 所在目录执行）。在任意支持 MCP 的客户端中，添加一个 `command` 类型的 MCP 服务器即可，例如：

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

## 📁 项目结构

```
NuGetMcpServer/
├── Program.cs                          # 入口：配置日志重定向到 stderr、注册服务
├── appsettings.json                    # 配置：NuGet:LocalPackagesPath
├── NuGetMcpServer.csproj               # 项目文件（.NET 10 + ModelContextProtocol 2.2.0）
├── Settings/
│   └── LocalSettingOptions.cs          # 本地包目录配置项及校验
├── Services/
│   ├── ServiceExtension.cs             # DI 注册：HttpClient、配置绑定、MCP 服务器
│   ├── NuGetCatalogClient.cs           # 在线查询 nuget.org v3-flatcontainer 目录 API
│   └── LocalNuGetPackageService.cs     # 扫描本地 .nupkg 目录，解析 包ID.版本 文件名
└── Tools/
    ├── NuGetTools.cs                   # 在线工具：get_latest_package_version / list_package_versions
    └── LocalNuGetTools.cs              # 本地工具：list_local_packages / get_local_package_versions
```

## 🧱 技术栈

- **.NET 10**（`net10.0`）
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) 2.2.0（官方 C# MCP SDK）
- [NuGet.Versioning](https://www.nuget.org/packages/NuGet.Versioning) 7.9.0（语义版本解析与排序）
- [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting)（宿主与依赖注入）

## 📄 License

[MIT](LICENSE.txt)
