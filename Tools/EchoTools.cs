using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NuGetMcpServer.Tools
{
    public sealed class EchoTools
    {
        [McpServerTool(Name = "echo")]
        [Description("重复返回它接收到的任何消息")]
        public static string Echo(
            [Description("要重复的消息")] string message) => "你说: " + message;

    }
}
