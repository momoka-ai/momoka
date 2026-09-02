using Momoka.Core.Plugins;

namespace Momoka.Core.Tests;

/// <summary>自宿主测试插件标记（供 Loader.Load 加载本测试程序集时的 manifest 主类）。</summary>
internal interface ISelfServiceMarker
{
}

internal sealed class SelfServiceImpl : ISelfServiceMarker
{
}

/// <summary>内嵌 plugin.toml（Resources/SelfPlugin.plugin.toml）声明的静态 Build 入口。</summary>
public static class SelfPluginEntry
{
    public static void Build(Plugin plugin)
    {
        plugin.AddService<ISelfServiceMarker>(new SelfServiceImpl());
    }
}
