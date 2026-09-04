using Momoka.Core.Commands;
using Momoka.Core.Events;
using Momoka.Core.Services;
using EventHandler = Momoka.Core.Events.EventHandler;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件运行时服务（唯一实例，宿主创建后持有）：当前启用插件的组合簿记与解析入口。
/// Add/Remove 由宿主在插件启停时按序调用；解析按"启用插件列表 + Service 描述符"LINQ 直查，
/// 无任何反查字典。单例实例由描述符自带的 ValueGetter 惰性维持（插件实例级保持）。
/// </summary>
public sealed class PluginService
{
    /// <summary>事件服务（订阅/发布走这里）。</summary>
    public EventService Events { get; } = new();

    /// <summary>当前启用插件（宿主启停按依赖拓扑序维护）。</summary>
    public List<Plugin> Plugins { get; } = new();

    /// <summary>登记插件：事件监听器注册进 <see cref="Events"/>，插件进入组合列表。</summary>
    public void Add(Plugin plugin)
    {
        foreach (EventHandler handler in plugin.EventHandlers)
        {
            Events.Add(handler);
        }

        Plugins.Add(plugin);
    }

    /// <summary>移除插件：事件监听器反注册，插件移出组合列表（其单例实例随描述符保留待复用）。</summary>
    public void Remove(Plugin plugin)
    {
        foreach (EventHandler handler in plugin.EventHandlers)
        {
            Events.Remove(handler);
        }

        Plugins.Remove(plugin);
    }

    /// <summary>按插件名查找（LINQ）。</summary>
    public Plugin? GetByName(string name) => Plugins.FirstOrDefault(p => p.Name == name);

    /// <summary>按注册命令反查来源插件（命令自带 Source）。</summary>
    public Plugin? GetByCommand(Command command) => command.Source;

    /// <summary>按服务契约类型反查正在提供该服务的插件（不取实例）。</summary>
    public Plugin? GetByService(Type serviceType) => FindService(serviceType)?.Plugin;

    /// <summary>同上，类型安全版本。</summary>
    public Plugin? GetByService<T>()
        where T : class
        => GetByService(typeof(T));

    /// <summary>在启用插件中查找服务契约类型对应的 Service 描述符。</summary>
    public Service? FindService(Type serviceType)
        => Plugins.SelectMany(p => p.Services, (p, s) => (s, p))
                  .FirstOrDefault(x => x.s.SourceType == serviceType).s;

    /// <summary>解析当前服务实例；未注册返回 null。Singleton 复用、Transient 新建。</summary>
    public T? Resolve<T>()
        where T : class
        => (T?)FindService(typeof(T))?.Value();

    /// <summary>解析当前服务实例；未注册返回 null。</summary>
    public object? Resolve(Type serviceType) => FindService(serviceType)?.Value();
}
