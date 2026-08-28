namespace NuGetMcpServer.Settings
{
    public sealed class LocalSettingOptions
    {
        public const string SectionName = "NuGet";

        /// <summary>
        /// 本地 NuGet 包目录路径，存放 {包ID}.{版本}.nupkg 文件。
        /// </summary>
        public string LocalPackagesPath { get; set; }


        public static bool Validate(LocalSettingOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.LocalPackagesPath))
                return false;
            return true;
        }
    }
}
