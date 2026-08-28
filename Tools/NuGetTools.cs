using ModelContextProtocol.Server;
using NuGetMcpServer.Services;
using System.ComponentModel;

namespace NuGetMcpServer.Tools
{
    [McpServerToolType] // 标记该类为工具类型，会自动注册到工具列表中
    public sealed class NuGetTools(NuGetCatalogClient catalog)
    {
        [McpServerTool(Name = "get_latest_package_version")]
        [Description("获取 NuGet 包的最新稳定版本。")]
        public async Task<string> GetLatestPackageVersionAsync(
            [Description("确切的 NuGet 包 ID，例如 ModelContextProtocol 或 Serilog.AspNetCore。")] string packageId, CancellationToken cancellationToken = default)
        {
            var versions = await catalog.GetVersionAsync(packageId, cancellationToken);

            if (versions.Count == 0)
                return $"未找到 ID 为 '{packageId}' 的 NuGet 包。请检查拼写。";

            var latestStable = versions.FirstOrDefault(x => !x.IsPrerelease);

            return latestStable is null ?
                $" {packageId} 尚未发布稳定版本，最新的预发布版本是 {versions[0]}。"
                : $" {packageId} 的最新稳定版本是 {latestStable}。";
        }

        [McpServerTool(Name = "list_package_versions")]
        [Description("列出 NuGet 包的最新版本，按最新优先，包括预发布版本。可用于检查特定版本是否存在。")]
        public async Task<string> ListPackageVersionAsync(
            [Description("确切的 NuGet 包 ID。")] string packageId,
            [Description("返回的版本数量，默认为10，最大为50。")] int count = 10,
            CancellationToken cancellationToken = default)
        {
            var versions = await catalog.GetVersionAsync(packageId, cancellationToken);
            if(versions.Count==0)
                return $"未找到 ID 为 '{packageId}' 的 NuGet 包。请检查拼写。";
            var take = Math.Clamp(count, 1, 50);
            var selected = versions.Take(take).Select(x => x.ToNormalizedString());

            return $"{packageId} 版本 (第一个为最新): {string.Join(", ", selected)}";
        }
    }
}
