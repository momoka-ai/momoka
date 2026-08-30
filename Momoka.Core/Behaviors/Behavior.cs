using Momoka.Core.Plugins;

namespace Momoka.Core.Behaviors;

/// <summary>
/// 行为基类：四件套契约 = 派生类型 + <c>Execute</c> + 嵌套 <c>Event</c>（事实 POD，
/// 携带 <see cref="PublishAttribute"/>，下行广播载荷，只由主机生成）+ 嵌套 <c>Intent</c>
/// （意图 POD，上行请求载荷，客户端唯一构造的对象）。
/// 派生类型由插件加载期扫描实例化（<see cref="Gateway.RegisterBehavior"/>，须公开无参构造器）并注入
/// <see cref="PluginService"/> 宿主；<c>Execute</c> 是逻辑执行函数（意图 → 事实），两端共用：
/// 主机在权威状态上执行并生成事实，客户端以收到的事实为意图在镜像上重放（幂等）。
/// 事实的唯一生成者是主机，结构上无环。
/// </summary>
public abstract class Behavior
{
    private PluginService? _host;

    /// <summary>宿主能力束（扫描期注入；访问未注入的宿主抛 <see cref="InvalidOperationException"/>）。</summary>
    protected PluginService Host => _host
        ?? throw new InvalidOperationException("Behavior host has not been injected yet.");

    /// <summary>注入宿主能力束（仅供 <see cref="Gateway.RegisterBehavior"/> 调用）。</summary>
    internal void InjectHost(PluginService host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }
}
