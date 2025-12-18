namespace FastGithub.HttpServer.Models
{
    /// <summary>
    /// 代理错误响应模型
    /// </summary>
    public class ProxyErrorResponse
    {
        /// <summary>
        /// 错误类型
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 初始化代理错误响应模型
        /// </summary>
        /// <param name="error">错误类型</param>
        /// <param name="message">错误消息</param>
        public ProxyErrorResponse(string error, string message)
        {
            Error = error;
            Message = message;
        }
    }
}