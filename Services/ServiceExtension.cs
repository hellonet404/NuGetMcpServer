using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGetMcpServer.Settings;
using NuGetMcpServer.Tools;

namespace NuGetMcpServer.Services
{
    public static class ServiceExtension
    {
        public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient<NuGetCatalogClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.nuget.org/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            // 通用 HttpClient 工厂，供 HttpTools 等工具使用
            services.AddHttpClient();

            // 读取 appsettings.json 中的 NuGet 配置节，并绑定到 LocalSettingOptions 类
            var nugetSection = configuration.GetSection(LocalSettingOptions.SectionName);
            services
                .AddOptions<LocalSettingOptions>()
                .Bind(nugetSection)
                .Validate(LocalSettingOptions.Validate, "LocalPackagesPath is required")
                .ValidateOnStart();


            // 本地 NuGet 包目录扫描服务，路径从 appsettings.json 的 NuGet:LocalPackagesPath 读取
            services.AddSingleton<LocalNuGetPackageService>();

            services
                .AddMcpServer()
                .WithStdioServerTransport()
                //.WithToolsFromAssembly(); //会扫描当前程序集中的 [McpServerToolType] 类
                .WithTools<NuGetTools>()
                .WithTools<LocalNuGetTools>()
                .WithTools<HttpTools>();

            return services;
        }
    }
}
