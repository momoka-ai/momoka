using System.Reflection;

namespace Momoka.Core.Plugins;

/// <summary>已加载插件文件的记录（文件级元数据，与 <see cref="Plugin"/> 实例一一对应）。</summary>
public sealed record PluginAssembly(string Path, PluginInfo Info, Assembly Assembly);
