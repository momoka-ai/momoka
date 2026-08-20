using Momoka.Home.Entities;
using Momoka.Home.Primitives;
namespace Momoka.Home.Level;

/// <summary>
/// 编辑工作单元：单线程使用（模型非线程安全；宿主网关负责命令串行化）。
/// 持有打开的 <see cref="LevelData"/>（类型 + 布局 + 注册表）；所有命令经
/// <see cref="Execute"/> 进入，成功产出 <see cref="ChangeSet"/>（实体增改删，
/// 供广播与客户端镜像）。撤销 / 重做不在本层——历史归客户端本地
/// （记录操作参数 + 重发逆操作请求）。
/// </summary>
public sealed class EditorSession
{
    public LevelData Data { get; private set; }
    public UnitLayout Layout => Data.Layout;

    public EditorSession(LevelData data)
    {
        Data = data;
    }

    /// <summary>
    /// 执行命令：校验 → 应用 → 组装 <see cref="ChangeSet"/> 返回。
    /// 返回 null 表示命令失败（无任何变更）。
    /// </summary>
    public ChangeSet? Execute(IEditorCommand command) =>
        command.Execute(this, out var changes) ? changes : null;

    /// <summary>替换打开的数据（<see cref="ServerLevelData.Load"/> 装载路径用）。</summary>
    public void Adopt(LevelData data)
    {
        Data = data;
    }
}
