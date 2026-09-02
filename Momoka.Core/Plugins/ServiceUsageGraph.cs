namespace Momoka.Core.Plugins;

/// <summary>
/// 服务使用有向图（[ServiceInjection] 注入时记录）：边 = 消费者插件经注入使用某服务，
/// 提供商 = 该服务当前的注册来源插件。供宿主 disable 守卫与 enable 排序使用——
/// 提供商仍有已启用消费者时禁止停用（fail-fast）；自注入（来源 = 自身）不构成边。
/// </summary>
public sealed class ServiceUsageGraph
{
    private readonly object _gate = new();
    private readonly Dictionary<Plugin, HashSet<Plugin>> _usersByProvider = new();

    /// <summary>记录一条使用边：<paramref name="consumer"/> 使用了 <paramref name="provider"/> 提供的服务。</summary>
    public void Add(Plugin consumer, Plugin provider)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(provider);
        if (ReferenceEquals(consumer, provider))
        {
            return;
        }

        lock (_gate)
        {
            if (!_usersByProvider.TryGetValue(provider, out var users))
            {
                users = new HashSet<Plugin>();
                _usersByProvider.Add(provider, users);
            }

            users.Add(consumer);
        }
    }

    /// <summary>直接使用 <paramref name="provider"/> 服务的全部消费者（快照）。</summary>
    public IReadOnlyList<Plugin> GetUsers(Plugin provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_gate)
        {
            return _usersByProvider.TryGetValue(provider, out var users)
                ? users.ToList()
                : new List<Plugin>();
        }
    }

    /// <summary>清空全部边（供测试隔离）。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _usersByProvider.Clear();
        }
    }
}
