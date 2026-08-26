namespace Momoka.Core;

/// <summary>网关配置（appsettings <c>Gateway</c> 节）。</summary>
public sealed class GatewayOptions
{
    /// <summary>
    /// 握手 token（query <c>token</c>，恒定时间比较）。缺省为空 = 拒绝全部连接。
    /// </summary>
    public string Token { get; set; } = string.Empty;
}
