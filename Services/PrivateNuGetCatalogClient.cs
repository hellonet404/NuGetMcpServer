using Microsoft.Extensions.Options;
using NuGet.Versioning;
using NuGetMcpServer.Settings;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace NuGetMcpServer.Services
{
    /// <summary>
    /// 私有 NuGet v3 仓库查询客户端（支持 Basic Auth，如阿里云云效制品仓库）。
    /// 查询流程：
    ///   1. 带认证 GET service index（index.json），解析出 PackageBaseAddress/3.0.0（flat container）资源地址；
    ///   2. 再带认证 GET {flatContainer}/{包ID}/index.json 获取版本列表。
    /// 认证头由 HttpClient 默认处理重定向：同主机重定向保留认证头，跨主机重定向丢弃（安全默认，不做特殊覆盖）。
    /// </summary>
    public sealed class PrivateNuGetCatalogClient(IHttpClientFactory httpClientFactory, IOptions<LocalSettingOptions> options)
    {
        /// <summary>列出所有已配置的私有源名称，供工具选择。</summary>
        public IReadOnlyList<string> GetSourceNames()
        {
            var sources = options.Value.PrivateSources;
            // 名称经 LocalSettingOptions.Validate 校验非空，此处用 null-forgiving 明确告知编译器
            return sources is null || sources.Count == 0
                ? []
                : sources.Select(s => s.Name!).ToList();
        }

        /// <summary>获取指定私有源中某包的全部版本（最新优先），未找到返回空列表。</summary>
        public async Task<IReadOnlyList<NuGetVersion>> GetVersionAsync(string sourceName, string packageId, CancellationToken cancellationToken = default)
        {
            var source = FindSource(sourceName);

            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Authorization = CreateBasicAuth(source);

            // 第 1 步：拉取 service index，解析 flat container 地址（ServiceIndexUrl 经配置校验非空）
            var flatContainerUrl = await GetFlatContainerUrlAsync(client, source.ServiceIndexUrl!, cancellationToken);

            // 第 2 步：拉取版本列表
            var id = packageId.Trim().ToLowerInvariant();
            using var response = await client.GetAsync(
                $"{flatContainerUrl.TrimEnd('/')}/{id}/index.json", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return [];

            response.EnsureSuccessStatusCode();

            var index = await response.Content.ReadFromJsonAsync(VersionIndexJsonContext.Default.VersionIndex, cancellationToken);

            if (index?.Versions is not { Count: > 0 } versions)
                return [];

            return [.. versions
                .Select(x => NuGetVersion.TryParse(x, out var parsed) ? parsed : null)
                .Where(x => x is not null)
                .Select(x => x!)
                .OrderByDescending(x => x, VersionComparer.VersionRelease)];
        }

        /// <summary>按名称查找私有源配置，找不到时抛出带可用列表的异常。</summary>
        private PrivateSourceOptions FindSource(string sourceName)
        {
            var sources = options.Value.PrivateSources;
            var source = sources?.FirstOrDefault(s =>
                string.Equals(s.Name, sourceName, StringComparison.OrdinalIgnoreCase));

            if (source is null)
            {
                var available = sources is null || sources.Count == 0
                    ? "(未配置任何私有源)"
                    : string.Join(", ", sources.Select(s => s.Name));
                throw new InvalidOperationException($"未找到名为 '{sourceName}' 的私有源。可用私有源: {available}");
            }
            return source;
        }

        /// <summary>构造 Basic Auth 认证头。</summary>
        private static AuthenticationHeaderValue CreateBasicAuth(PrivateSourceOptions source)
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{source.Username}:{source.Password}"));
            return new AuthenticationHeaderValue("Basic", token);
        }

        /// <summary>带认证拉取 service index，解析 PackageBaseAddress/3.0.0 资源地址。</summary>
        private static async Task<string> GetFlatContainerUrlAsync(HttpClient client, string serviceIndexUrl, CancellationToken cancellationToken)
        {
            using var response = await client.GetAsync(serviceIndexUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var index = await response.Content.ReadFromJsonAsync(PrivateServiceIndexJsonContext.Default.ServiceIndex, cancellationToken)
                ?? throw new InvalidOperationException("service index 响应为空");

            var resource = index.Resources?.FirstOrDefault(r =>
                string.Equals(r.Type, "PackageBaseAddress/3.0.0", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(resource?.Id))
                throw new InvalidOperationException("service index 中未找到 PackageBaseAddress/3.0.0 资源");

            return resource.Id;
        }
    }

    /// <summary>
    /// NuGet v3 service index（index.json）响应模型。
    /// 只需解析 resources 中 PackageBaseAddress/3.0.0 的 @id。
    /// </summary>
    internal sealed record ServiceIndex(
        [property: JsonPropertyName("resources")] IReadOnlyList<ServiceIndexResource>? Resources);

    internal sealed record ServiceIndexResource(
        [property: JsonPropertyName("@id")] string? Id,
        [property: JsonPropertyName("@type")] string? Type);

    /// <summary>
    /// AOT 发布所需的 System.Text.Json 序列化元数据（source generator 生成）。
    /// </summary>
    [JsonSerializable(typeof(ServiceIndex))]
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    internal sealed partial class PrivateServiceIndexJsonContext : JsonSerializerContext
    {
    }
}
