using Momoka.Home.Data.Sqlite;
namespace Momoka.Home.Editing;

public sealed class ChangeSetEventArgs : EventArgs
{
    public ChangeSet Changes { get; }
    public ChangeSetEventArgs(ChangeSet changes) => Changes = changes;
}

/// <summary>
/// 编辑工作单元：单线程使用（模型非线程安全；宿主网关负责命令串行化）。
/// 持有打开的存档（<see cref="Residence"/>）、命令历史（<see cref="History"/>）与
/// Region 增量维护器（<see cref="Regions"/>）；所有命令经 <see cref="Execute"/> 进入。
/// 执行 / 撤销 / 重做后统一组装 <see cref="ChangeSet"/>（实体增改删 + 受影响 Region，
/// 见 <see cref="RegionMaintainer"/>）并发出 <see cref="LayoutChanged"/>——
/// 命令层与受控写格共用一条变更管道。脏区块不在服务端计算（协议线不携带，
/// 由 <see cref="ClientLevelData"/> 本地推导）。
/// </summary>
public sealed class EditorSession
{
    public Residence Residence { get; private set; }
    public UnitLayout Layout => Residence.Layout;
    public CommandHistory History { get; } = new();
    public RegionMaintainer Regions { get; }

    public event EventHandler<ChangeSetEventArgs>? LayoutChanged;

    public EditorSession(Residence residence)
    {
        Residence = residence;
        Regions = new RegionMaintainer();
    }

    /// <summary>从存档加载（residence + entities），返回就绪会话；无存档返回 null。</summary>
    public static EditorSession? Open(SqliteStore store)
    {
        var residence = store.Load();
        return residence is null ? null : new EditorSession(residence);
    }

    public void Save(SqliteStore store) => store.Save(Residence);

    /// <summary>全量重建 Region 层（首次打开 / 加载后调用；结构变更由 <see cref="RegionMaintainer"/> 自动触发）。</summary>
    public void RebuildRegions() => Regions.Rebuild(Layout);

    /// <summary>
    /// 执行命令：校验 → 执行 → 逆操作入 History（或按 CoalesceKey 合并）→
    /// 计算 ChangeSet（脏块 + Region 增量）→ 触发 <see cref="LayoutChanged"/>。
    /// 返回 null 表示命令失败（无任何变更，不推历史）。
    /// </summary>
    public ChangeSet? Execute(IEditorCommand command)
    {
        if (!command.Execute(this, out var changes))
            return null;
        History.Record(command);
        Finish(changes);
        return changes;
    }

    /// <summary>撤销最近命令（或最近一个合并历史项），返回其变更集；无可撤销返回 null。</summary>
    public ChangeSet? Undo()
    {
        var changes = History.Undo(this);
        if (changes is not null)
            Finish(changes);
        return changes;
    }

    /// <summary>重做最近撤销，返回其变更集；无可重做返回 null。</summary>
    public ChangeSet? Redo()
    {
        var changes = History.Redo(this);
        if (changes is not null)
            Finish(changes);
        return changes;
    }

    /// <summary>
    /// 替换打开的存档（<see cref="ServerLevelData.Load"/> 装载路径用）：
    /// 清空历史并重置 Region 增量维护基线（持久化 Region 由 <see cref="RegionMaintainer.Adopt"/>
    /// 注入，不走全量重建）。
    /// </summary>
    public void Adopt(Residence residence)
    {
        Residence = residence;
        History.Clear();
        Regions.Reset();
    }

    /// <summary>
    /// 变更收尾：Region 增量维护（受影响 Region 集入 ChangeSet）+ 触发
    /// <see cref="LayoutChanged"/>。脏区块不在服务端计算——协议线不携带，
    /// 由 <see cref="ClientLevelData"/> 凭实体旧/新格本地推导。
    /// </summary>
    private void Finish(ChangeSet changes)
    {
        Regions.ApplyChange(Layout, changes);
        LayoutChanged?.Invoke(this, new ChangeSetEventArgs(changes));
    }
}
