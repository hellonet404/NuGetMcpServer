using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NuGetMcpServer.Services;

namespace NuGetMcpServer.Api;

/// <summary>
/// REST 端点：包装与 MCP 工具相同的服务，便于在 Scalar/Swagger 中本地调试查询。
/// 所有响应均使用具名 record（而非匿名类型），配合 AppJsonSerializerContext 实现 AOT 安全的序列化。
/// </summary>
public static class NuGetApiEndpoints
{
    public static IEndpointRouteBuilder MapNuGetApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
            .WithName("HealthCheck")
            .WithSummary("健康检查")
            .WithDescription("返回服务是否正常运行。");

        group.MapGet("/nuget/latest", async (string packageId, NuGetCatalogClient catalog, CancellationToken ct) =>
        {
            var versions = await catalog.GetVersionAsync(packageId, ct);
            if (versions.Count == 0)
                return Results.NotFound(new ErrorResponse($"未找到 ID 为 '{packageId}' 的 NuGet 包。"));

            var latestStable = versions.FirstOrDefault(v => !v.IsPrerelease);
            return Results.Ok(new LatestVersionResponse(packageId, latestStable?.ToNormalizedString(), versions[0].ToNormalizedString()));
        })
        .WithName("GetLatestPackageVersion")
        .WithSummary("获取 NuGet 包最新稳定版本")
        .WithDescription("查询 nuget.org 上指定包的最新稳定版本；若无稳定版则返回最新预发布版。");

        group.MapGet("/nuget/versions", async (string packageId, int count, NuGetCatalogClient catalog, CancellationToken ct) =>
        {
            var versions = await catalog.GetVersionAsync(packageId, ct);
            if (versions.Count == 0)
                return Results.NotFound(new ErrorResponse($"未找到 ID 为 '{packageId}' 的 NuGet 包。"));

            var list = versions
                .Take(count > 0 ? count : 10)
                .Select(v => new VersionInfo(v.ToNormalizedString(), v.IsPrerelease))
                .ToList();

            return Results.Ok(new VersionListResponse(packageId, list));
        })
        .WithName("ListPackageVersions")
        .WithSummary("列出 NuGet 包版本（最新优先，含预发布）")
        .WithDescription("查询 nuget.org 上指定包的版本列表，按最新优先排序。");

        group.MapGet("/nuget/local", (string? search, LocalNuGetPackageService local, CancellationToken ct) =>
        {
            var all = local.GetAllPackages(ct);

            IEnumerable<KeyValuePair<string, IReadOnlyList<NuGet.Versioning.NuGetVersion>>> items = all;
            if (!string.IsNullOrWhiteSpace(search))
                items = items.Where(pair => pair.Key.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

            var list = items
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new LocalPackageInfo(pair.Key, pair.Value.Select(v => v.ToNormalizedString()).ToList()))
                .ToList();

            return Results.Ok(new LocalPackageListResponse(all.Count, list));
        })
        .WithName("ListLocalPackages")
        .WithSummary("列出本地 NuGet 包")
        .WithDescription("扫描本地包目录（NuGet:LocalPackagesPath），按包 ID 过滤，最新优先。");

        group.MapGet("/nuget/private/sources", (PrivateNuGetCatalogClient client) =>
            Results.Ok(new PrivateSourcesResponse(client.GetSourceNames().ToList())))
            .WithName("ListPrivateSources")
            .WithSummary("列出私有 NuGet 源")
            .WithDescription("返回 appsettings.json 中配置的私有源名称。");

        group.MapGet("/nuget/private/versions", async (string sourceName, string packageId, PrivateNuGetCatalogClient client, CancellationToken ct) =>
        {
            var versions = await client.GetVersionAsync(sourceName, packageId, ct);
            if (versions.Count == 0)
                return Results.NotFound(new ErrorResponse($"私有源 '{sourceName}' 中未找到 ID 为 '{packageId}' 的 NuGet 包。"));

            var list = versions.Select(v => new VersionInfo(v.ToNormalizedString(), v.IsPrerelease)).ToList();
            return Results.Ok(new VersionListResponse(packageId, list));
        })
        .WithName("ListPrivatePackageVersions")
        .WithSummary("列出私有源中包版本")
        .WithDescription("在指定私有 NuGet 源中查询包版本，最新优先。");

        return app;
    }
}
