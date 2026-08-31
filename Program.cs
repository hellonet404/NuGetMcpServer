using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGetMcpServer.Api;
using NuGetMcpServer.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// stdout 承载 MCP JSON-RPC 流（stdio 传输保持原样）。
// 任何写入 stdout 的内容都会破坏协议，因此所有日志统一写到 stderr。
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// 注册现有服务（含 stdio MCP 服务器）与 OpenAPI 文档生成。
builder.Services.AddServices(builder.Configuration);
builder.Services.AddOpenApi();

// AOT 安全 JSON 序列化：将本项目 DTO 的源生成上下文插入解析器链首位，
// 与 Microsoft.AspNetCore.OpenApi 生成的 OpenApiJsonSchemaContext 组合使用，
// 避免匿名类型 / 未注册类型运行时抛 NotSupportedException。
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();

// OpenAPI / Scalar（最新 swagger UI）
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// 可测试访问的 REST 接口：包装与 MCP 工具相同的服务。
app.MapNuGetApiEndpoints();

app.Run();