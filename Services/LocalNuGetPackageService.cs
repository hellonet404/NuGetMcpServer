using Microsoft.Extensions.Options;
using NuGet.Versioning;
using NuGetMcpServer.Settings;

namespace NuGetMcpServer.Services
{
    /// <summary>
    /// 扫描本地扁平 NuGet 包目录（存放 {包ID}.{版本}.nupkg 文件），
    /// 通过解析文件名得到 包ID + 版本。
    /// 目录路径从配置 NuGet:LocalPackagesPath 读取。
    /// </summary>
    public sealed class LocalNuGetPackageService(IOptions<LocalSettingOptions> options)
    {
        /// <summary>
        /// 扫描目录，返回 包ID -> 版本列表（最新优先）的映射。
        /// 目录不存在或无包时返回空字典。
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<NuGetVersion>> GetAllPackages(CancellationToken cancellationToken = default)
        {
            var path = options.Value.LocalPackagesPath;
            if (!Directory.Exists(path))
                return new Dictionary<string, IReadOnlyList<NuGetVersion>>();

            var byId = new Dictionary<string, List<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(path, "*.nupkg", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!TryParsePackageFileName(fileName, out var packageId, out var version))
                    continue;

                if (!byId.TryGetValue(packageId, out var versions))
                    byId[packageId] = versions = new List<NuGetVersion>();
                versions.Add(version);
            }

            // 每个包的版本按从新到旧排序
            var result = new Dictionary<string, IReadOnlyList<NuGetVersion>>(byId.Count);
            foreach (var pair in byId)
            {
                pair.Value.Sort(Comparer<NuGetVersion>.Create((a, b) => VersionComparer.VersionRelease.Compare(b, a)));
                result[pair.Key] = pair.Value;
            }
            return result;
        }

        /// <summary>获取指定包 ID 的本地版本列表（最新优先），未找到返回空列表。</summary>
        public IReadOnlyList<NuGetVersion> GetVersions(string packageId, CancellationToken cancellationToken = default)
        {
            var all = GetAllPackages(cancellationToken);
            return all.TryGetValue(packageId, out var versions) ? versions : [];
        }

        /// <summary>
        /// 解析形如 {包ID}.{版本} 的文件名（不含扩展名）。
        /// 从左向右扫描切分点，取第一个能让后缀完整解析为 NuGet 版本的切分位置：
        /// 这等价于取"最长可解析版本后缀"，从而把尽量多的部分保留为包 ID（包 ID 本身可含点号）。
        /// </summary>
        private static bool TryParsePackageFileName(string fileName, out string packageId, out NuGetVersion version)
        {
            packageId = null!;
            version = null!;

            for (var i = 0; i < fileName.Length; i++)
            {
                if (fileName[i] != '.')
                    continue;

                var candidate = fileName[(i + 1)..];
                if (NuGetVersion.TryParse(candidate, out var parsed))
                {
                    packageId = fileName[..i];
                    version = parsed;
                    return true;
                }
            }
            return false;
        }
    }
}
