using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuGetMcpServer.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    // stdout carries the JSON-RPC stream. Anything else written there corrupts the
    // protocol, so every log goes to stderr instead.
    // 标准输出流承载着 JSON-RPC 数据流。任何写入该流的内容都会破坏协议，因此所有日志都会转而写入标准错误流。
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddServices(builder.Configuration);

await builder.Build().RunAsync();