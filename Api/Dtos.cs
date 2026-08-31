namespace NuGetMcpServer.Api;

/// <summary>健康检查响应。</summary>
public sealed record HealthResponse(string Status);

/// <summary>错误响应。</summary>
public sealed record ErrorResponse(string Message);

/// <summary>私有 NuGet 源名称列表响应。</summary>
public sealed record PrivateSourcesResponse(List<string> Sources);

/// <summary>最新稳定版本响应。</summary>
public sealed record LatestVersionResponse(string PackageId, string? LatestStable, string Latest);

/// <summary>单个版本信息。</summary>
public sealed record VersionInfo(string Version, bool IsPrerelease);

/// <summary>版本列表响应。</summary>
public sealed record VersionListResponse(string PackageId, List<VersionInfo> Versions);

/// <summary>本地包信息。</summary>
public sealed record LocalPackageInfo(string PackageId, List<string> Versions);

/// <summary>本地包列表响应。</summary>
public sealed record LocalPackageListResponse(int TotalCount, List<LocalPackageInfo> Packages);
