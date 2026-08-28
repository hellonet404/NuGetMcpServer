using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NuGetMcpServer.Tools
{
    /// <summary>
    /// 通用 HTTP 工具：让 MCP 客户端通过本服务器访问 HTTPS 资源。
    /// 用于绕过沙箱环境的网络限制（沙箱内 .NET/curl 的 HTTPS 被拦截，但 MCP 服务器进程可正常访问 HTTPS）。
    /// </summary>
    [McpServerToolType]
    public sealed class HttpTools(IHttpClientFactory httpClientFactory)
    {
        [McpServerTool(Name = "http_get_text")]
        [Description("通过 GET 请求获取指定 HTTPS URL 的文本内容，用于访问被沙箱网络策略限制的 HTTPS 资源（如 NuGet API、JSON 接口等）。")]
        public async Task<string> GetTextAsync(
            [Description("要访问的 HTTPS URL，必须以 https:// 开头。")] string url,
            CancellationToken cancellationToken = default)
        {
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return "仅支持 https:// 协议的 URL。";

            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            return await client.GetStringAsync(url, cancellationToken);
        }

        [McpServerTool(Name = "http_download")]
        [Description("从指定 HTTPS URL 下载内容并保存到本地文件，用于下载 NuGet 包（.nupkg）等二进制资源。")]
        public async Task<string> DownloadAsync(
            [Description("要下载的 HTTPS URL，必须以 https:// 开头。")] string url,
            [Description("保存文件的完整本地路径，例如 D:\\Code\\NugetPackages\\Some.Package.1.0.0.nupkg。")] string outputPath,
            CancellationToken cancellationToken = default)
        {
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return "仅支持 https:// 协议的 URL。";

            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(300);

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await using (var fs = File.Create(outputPath))
            {
                await response.Content.CopyToAsync(fs, cancellationToken);
            }

            var length = response.Content.Headers.ContentLength ?? new FileInfo(outputPath).Length;
            return $"已下载 {length} 字节到 {outputPath}";
        }
    }
}
