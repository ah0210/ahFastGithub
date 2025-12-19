using FastGithub.HttpServer.Models;
using System.Text.Json.Serialization;

namespace FastGithub.HttpServer.Json
{
    /// <summary>
    /// HTTP服务器JSON上下文
    /// </summary>
    [JsonSerializable(typeof(ProxyErrorResponse))]
    public partial class HttpServerJsonContext : JsonSerializerContext
    {
    }
}