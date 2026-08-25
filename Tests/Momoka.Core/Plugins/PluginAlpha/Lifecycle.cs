namespace Momoka.Core.Tests.Plugins.Alpha;

/// <summary>测试夹具：把插件生命周期事件追加到宿主进程基目录的共享日志文件。</summary>
internal static class Lifecycle
{
    private static readonly string FilePath =
        Path.Combine(AppContext.BaseDirectory, "plugin-lifecycle.log");

    public static void Record(string plugin, string action)
    {
        File.AppendAllText(FilePath, $"{plugin}:{action}\n");
    }
}
