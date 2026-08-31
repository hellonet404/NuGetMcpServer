namespace NuGetMcpServer.Settings
{
    public sealed class LocalSettingOptions
    {
        public const string SectionName = "NuGet";

        /// <summary>
        /// 本地 NuGet 包目录路径，存放 {包ID}.{版本}.nupkg 文件。
        /// </summary>
        public string LocalPackagesPath { get; set; }

        /// <summary>
        /// 需要认证的私有 NuGet v3 仓库列表（如阿里云云效制品仓库）。
        /// 每个仓库独立配置 Name / ServiceIndexUrl / Username / Password。
        /// </summary>
        public List<PrivateSourceOptions> PrivateSources { get; set; } = [];


        public static bool Validate(LocalSettingOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.LocalPackagesPath))
                return false;

            if (options.PrivateSources is not null)
            {
                foreach (var source in options.PrivateSources)
                {
                    if (string.IsNullOrWhiteSpace(source.Name) ||
                        string.IsNullOrWhiteSpace(source.ServiceIndexUrl) ||
                        string.IsNullOrWhiteSpace(source.Username) ||
                        string.IsNullOrWhiteSpace(source.Password))
                        return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 单个私有 NuGet v3 仓库配置：service index 地址 + Basic Auth 凭证。
    /// </summary>
    public sealed class PrivateSourceOptions
    {
        /// <summary>仓库唯一名称，工具用 sourceName 参数选择。</summary>
        public string Name { get; set; }

        /// <summary>NuGet v3 service index 地址，例如 https://packages.aliyun.com/{企业ID}/nuget/repo-xxx/index.json。</summary>
        public string ServiceIndexUrl { get; set; }

        /// <summary>Basic Auth 用户名（阿里云云效为个人/系统凭证用户名）。</summary>
        public string Username { get; set; }

        /// <summary>Basic Auth 密码。</summary>
        public string Password { get; set; }
    }
}
