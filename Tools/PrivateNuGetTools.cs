using ModelContextProtocol.Server;
using NuGet.Versioning;
using NuGetMcpServer.Services;
using System.ComponentModel;

namespace NuGetMcpServer.Tools
{
    /// <summary>
    /// 私有 NuGet 仓库查询工具（支持 Basic Auth，如阿里云云效制品仓库）。
    /// 仓库与凭证通过 appsettings.json 的 NuGet:PrivateSources 配置。
    /// </summary>
    [McpServerToolType]
    public sealed class PrivateNuGetTools(PrivateNuGetCatalogClient catalog)
    {
        [McpServerTool(Name = "list_private_sources")]
        [Description("列出所有已配置的私有 NuGet 仓库名称（如阿里云云效制品仓库），作为其他私有仓库工具的 sourceName 参数。")]
        public string ListPrivateSources()
        {
            var names = catalog.GetSourceNames();
            return names.Count == 0
                ? "未配置任何私有 NuGet 仓库。请在 appsettings.json 的 NuGet:PrivateSources 中配置。"
                : $"已配置的私有 NuGet 仓库: {string.Join(", ", names)}";
        }

        [McpServerTool(Name = "get_private_latest_package_version")]
        [Description("获取私有 NuGet 仓库中指定包的最新稳定版本（若尚无稳定版则提示最新预发布版本）。需要先调用 list_private_sources 获取可用的仓库名称。")]
        public async Task<string> GetLatestPackageVersionAsync(
            [Description("私有 NuGet 仓库名称，来自 list_private_sources。")] string sourceName,
            [Description("确切的 NuGet 包 ID，例如 ModelContextProtocol 或 Serilog.AspNetCore。")] string packageId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var versions = await catalog.GetVersionAsync(sourceName, packageId, cancellationToken);

                if (versions.Count == 0)
                    return $"在私有仓库 '{sourceName}' 中未找到 ID 为 '{packageId}' 的 NuGet 包。请检查拼写。";

                var latestStable = versions.FirstOrDefault(x => !x.IsPrerelease);

                return latestStable is null ?
                    $" {packageId} 在私有仓库 '{sourceName}' 中尚未发布稳定版本，最新的预发布版本是 {versions[0]}。"
                    : $" {packageId} 在私有仓库 '{sourceName}' 中的最新稳定版本是 {latestStable}。";
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
            catch (HttpRequestException ex)
            {
                return $"访问私有仓库 '{sourceName}' 失败: {ex.Message}";
            }
        }

        [McpServerTool(Name = "list_private_package_versions")]
        [Description("列出私有 NuGet 仓库中指定包的最新版本，按最新优先，包括预发布版本。可用于检查特定版本是否已发布到私有仓库。需要先调用 list_private_sources 获取可用的仓库名称。")]
        public async Task<string> ListPackageVersionsAsync(
            [Description("私有 NuGet 仓库名称，来自 list_private_sources。")] string sourceName,
            [Description("确切的 NuGet 包 ID。")] string packageId,
            [Description("返回的版本数量，默认为10，最大为50。")] int count = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var versions = await catalog.GetVersionAsync(sourceName, packageId, cancellationToken);
                if (versions.Count == 0)
                    return $"在私有仓库 '{sourceName}' 中未找到 ID 为 '{packageId}' 的 NuGet 包。请检查拼写。";

                var take = Math.Clamp(count, 1, 50);
                var selected = versions.Take(take).Select(x => x.ToNormalizedString());

                return $"{packageId} 在私有仓库 '{sourceName}' 中的版本 (第一个为最新): {string.Join(", ", selected)}";
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
            catch (HttpRequestException ex)
            {
                return $"访问私有仓库 '{sourceName}' 失败: {ex.Message}";
            }
        }
    }
}
