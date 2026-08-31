using System.Text.Json.Serialization;

namespace NuGetMcpServer.Api;

/// <summary>
/// AOT 安全的 JSON 序列化元数据（source generator 生成）。
/// 注册所有 REST 端点返回的 DTO 类型；与 Microsoft.AspNetCore.OpenApi 的
/// OpenApiJsonSchemaContext 通过 TypeInfoResolverChain 组合使用。
/// </summary>
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(PrivateSourcesResponse))]
[JsonSerializable(typeof(LatestVersionResponse))]
[JsonSerializable(typeof(VersionInfo))]
[JsonSerializable(typeof(VersionListResponse))]
[JsonSerializable(typeof(LocalPackageInfo))]
[JsonSerializable(typeof(LocalPackageListResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}
