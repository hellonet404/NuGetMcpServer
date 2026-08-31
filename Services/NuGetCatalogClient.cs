using NuGet.Versioning;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NuGetMcpServer.Services
{
    public sealed class NuGetCatalogClient(HttpClient httpClient)
    {
        public async Task<IReadOnlyList<NuGetVersion>> GetVersionAsync(string packageId, CancellationToken cancellationToken = default)
        {
            // 包的名称转换为小写
            var id = packageId.Trim().ToLowerInvariant();

            using var response = await httpClient.GetAsync(
                $"v3-flatcontainer/{id}/index.json"
                , cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return [];

            response.EnsureSuccessStatusCode();

            // AOT 安全：使用 source generator 生成的 JsonTypeInfo，避免反射式 System.Text.Json
            var index = await response.Content.ReadFromJsonAsync(VersionIndexJsonContext.Default.VersionIndex, cancellationToken);

            if(index?.Versions is not { Count: > 0 } versions)
                return [];

            return [.. versions
                .Select(x=>  NuGetVersion.TryParse(x, out var parsed)? parsed: null)
                .Where(x=> x is not null)
                .Select(x=> x!)
                .OrderByDescending(x=>x, VersionComparer.VersionRelease)];
        }


    }

    /// <summary>
    /// v3-flatcontainer/{id}/index.json 响应模型。
    /// [property: JsonPropertyName("versions")] 明确告诉编译器"这个特性要贴到属性上，而不是别的成员上
    /// </summary>
    internal sealed record VersionIndex([property: JsonPropertyName("versions")] IReadOnlyList<string> Versions);

    /// <summary>
    /// AOT 发布所需的 System.Text.Json 序列化元数据（source generator 生成）。
    /// </summary>
    [JsonSerializable(typeof(VersionIndex))]
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    internal sealed partial class VersionIndexJsonContext : JsonSerializerContext
    {
    }
}
