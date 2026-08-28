using ModelContextProtocol.Server;
using NuGet.Versioning;
using NuGetMcpServer.Services;
using System.ComponentModel;

namespace NuGetMcpServer.Tools
{
    /// <summary>
    /// 本地 NuGet 包查询工具
    /// </summary>
    [McpServerToolType]
    public sealed class LocalNuGetTools(LocalNuGetPackageService local)
    {
        [McpServerTool(Name = "list_local_packages")]
        [Description("列出本地 NuGet 包目录中的所有包及其版本，最新版本优先。可按包 ID 过滤。")]
        public string ListLocalPackages(
            [Description("按包 ID 过滤（不区分大小写，支持子串匹配），可选，留空则列出全部。")] string search = "",
            CancellationToken cancellationToken = default)
        {
            var all = local.GetAllPackages(cancellationToken);

            if (all.Count == 0)
                return $"本地 NuGet 包目录为空。";

            IEnumerable<KeyValuePair<string, IReadOnlyList<NuGetVersion>>> items = all;
            if (!string.IsNullOrWhiteSpace(search))
                items = items.Where(pair => pair.Key.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

            var matched = items.ToList();
            if (matched.Count == 0)
                return $"未找到包含 '{search}' 的本地 NuGet 包。";

            var lines = matched
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key} ({string.Join(", ", pair.Value.Select(v => v.ToNormalizedString()))})");

            return $"本地共 {all.Count} 个包，命中 {matched.Count} 个：\n{string.Join("\n", lines)}";
        }

        [McpServerTool(Name = "get_local_package_versions")]
        [Description("列出指定 NuGet 包在本地目录中的所有版本，最新优先。可用于确认某版本是否已在本地。")]
        public string GetLocalPackageVersions(
            [Description("确切的 NuGet 包 ID，例如 CRTVUP.Learning.CourseService.Implement。")] string packageId,
            CancellationToken cancellationToken = default)
        {
            var versions = local.GetVersions(packageId, cancellationToken);

            if (versions.Count == 0)
                return $"本地目录中未找到 ID 为 '{packageId}' 的 NuGet 包。";

            return $"{packageId} 本地版本 (第一个为最新): {string.Join(", ", versions.Select(v => v.ToNormalizedString()))}";
        }
    }
}
