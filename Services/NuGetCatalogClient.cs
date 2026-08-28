using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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

            var index = await response.Content.ReadFromJsonAsync<VersionIndex>(cancellationToken);

            if(index?.Versions is not { Count: > 0 } versions)
                return [];

            return [.. versions
                .Select(x=>  NuGetVersion.TryParse(x, out var parsed)? parsed: null)
                .Where(x=> x is not null)
                .Select(x=> x!)
                .OrderByDescending(x=>x, VersionComparer.VersionRelease)];
        }


        private sealed record VersionIndex([property: JsonPropertyName("versions")] IReadOnlyList<string> Versions);
    }
}
